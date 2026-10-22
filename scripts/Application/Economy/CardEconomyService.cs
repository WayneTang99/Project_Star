using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Economy;

public enum CardAcquisitionSource
{
    Purchase = 0,
    Drop = 1,
    Reward = 2,
}

// 一次获得卡牌后的创建或合并结果（应用层经济模块）。
public sealed record CardAcquisitionResult(CardInstance Card, bool WasCreated, int PreviousLevel, int CurrentLevel)
{
    public bool WasUpgraded => !WasCreated;
    public EntityId Id => Card.Id;
    public EntityAttributes<CardIdentityAttributes> Attributes => Card.Attributes;
    public IReadOnlyList<Project_Star.Domain.Combat.AbilityDefinition> Abilities => Card.Abilities;
    public static implicit operator CardInstance(CardAcquisitionResult result) => result.Card;
}

// 应用层统一获得、购买、放置与出售；先校验完整操作，再同步提交。
public sealed class CardEconomyService
{
    private static readonly StringName InsufficientWealth = new("economy.insufficient_wealth");
    private static readonly StringName CardNotOwned = new("economy.card_not_owned");
    private static readonly StringName CardOnBoard = new("economy.card_on_board");
    private static readonly StringName InvalidValue = new("economy.invalid_value");
    private static readonly StringName OfferSold = new("economy.offer_sold");
    private static readonly StringName SaleRewardUnavailable = new("economy.sale_reward_unavailable");
    private static readonly StringName MergeBoardUnavailable = new("economy.merge_board_unavailable");
    private static readonly StringName BoardFull = new("economy.board_full");

    private readonly EntityFactory _entityFactory;
    private readonly BoardService? _boardService;
    private readonly IReadOnlyList<CardDefinition> _cardDefinitions;

    public CardEconomyService(
        EntityFactory entityFactory,
        BoardService? boardService = null,
        IEnumerable<CardDefinition>? cardDefinitions = null)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        _boardService = boardService;
        _cardDefinitions = cardDefinitions is null
            ? Array.Empty<CardDefinition>()
            : cardDefinitions
                .OrderBy(definition => definition.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
                .ToArray();
    }

    public Result<CardAcquisitionResult> AcquireCard(
        MatchSession session,
        CardDefinition definition,
        int level,
        CardAcquisitionSource source)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);
        _ = source;

        var plan = BuildMergePlan(session, definition, level);
        if (!CanApplyMergePlan(session, plan))
            return Result<CardAcquisitionResult>.Fail(new Failure(
                MergeBoardUnavailable,
                "Merging placed cards requires configured board operations."));
        var valueCheck = CheckMergeValue(session, definition, plan);
        if (valueCheck.IsFailure) return Result<CardAcquisitionResult>.Fail(valueCheck.Failure!);
        var acquired = AcquireOrMerge(session, definition, level, plan);
        ApplyPickup(session, acquired.Card);
        return Result<CardAcquisitionResult>.Success(acquired);
    }

    public Result<CardAcquisitionResult> BuyCard(MatchSession session, ShopOffer offer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(offer);

        if (offer.IsSold)
        {
            return Result<CardAcquisitionResult>.Fail(new Failure(OfferSold, "This shop offer has already been purchased."));
        }

        if (session.Player.Wealth < offer.Price)
        {
            return Result<CardAcquisitionResult>.Fail(new Failure(InsufficientWealth, "Not enough wealth to buy this card."));
        }

        var plan = BuildMergePlan(session, offer.Definition, offer.Level);
        if (!CanApplyMergePlan(session, plan))
            return Result<CardAcquisitionResult>.Fail(new Failure(
                MergeBoardUnavailable,
                "Merging placed cards requires configured board operations."));

        var valueCheck = CheckMergeValue(session, offer.Definition, plan);
        if (valueCheck.IsFailure) return Result<CardAcquisitionResult>.Fail(valueCheck.Failure!);
        if (!session.Player.TrySpendWealth(offer.Price))
        {
            return Result<CardAcquisitionResult>.Fail(new Failure(InsufficientWealth, "Not enough wealth to buy this card."));
        }

        var result = AcquireOrMerge(session, offer.Definition, offer.Level, plan);
        offer.MarkSold();
        ApplyPickup(session, result.Card);
        return Result<CardAcquisitionResult>.Success(result);
    }

    // 只读校验完整购买条件，与界面展示和实际提交共用。
    public Result CheckPurchase(MatchSession session, ShopOffer offer)
    {
        if (offer.IsSold) return Result.Fail(new Failure(OfferSold, "该商品已售罄。"));
        if (session.Player.Wealth < offer.Price)
            return Result.Fail(new Failure(InsufficientWealth, "金钱不足。"));
        return CheckPlacement(session, offer.Definition, BuildMergePlan(session, offer.Definition, offer.Level));
    }

    // 一次购买完成扣款、合并或自动放置，以及售罄标记；失败不改变状态。
    public Result<CardAcquisitionResult> BuyAndPlace(MatchSession session, ShopOffer offer)
    {
        var check = CheckPurchase(session, offer);
        if (check.IsFailure) return Result<CardAcquisitionResult>.Fail(check.Failure!);
        var acquired = AcquireAndPlace(session, offer.Definition, offer.Level, CardAcquisitionSource.Purchase);
        if (acquired.IsFailure) return acquired;
        session.Player.TrySpendWealth(offer.Price);
        offer.MarkSold();
        return acquired;
    }

    // 获得奖励与购买共用合并、空间预检和自动放置，不留游离库存。
    public Result<CardAcquisitionResult> AcquireAndPlace(MatchSession session, CardDefinition definition,
        int level, CardAcquisitionSource source, SeededRandom? random = null)
    {
        var plan = BuildMergePlan(session, definition, level);
        var check = CheckPlacement(session, definition, plan);
        if (check.IsFailure) return Result<CardAcquisitionResult>.Fail(check.Failure!);
        var target = plan.TargetId is { } id && session.Board.Contains(id) ? null
            : _boardService!.FindFirstAvailableTarget(session, definition.Attributes.Identity.OccupiedSlots, plan.ConsumedIds);
        var acquired = AcquireOrMerge(session, definition, level, plan);
        if (target is not null)
        {
            // 同步提交期间没有外部回调；预检后只会释放空间，放置计划保持有效。
            var placed = _boardService!.PlaceCard(session, acquired.Id, target.Zone, target.Start);
            if (placed.IsFailure) throw new InvalidOperationException(placed.Failure!.Message);
        }
        else _boardService!.RefreshBonuses(session);
        ApplyPickup(session, acquired.Card, random);
        return Result<CardAcquisitionResult>.Success(acquired);
    }

    // 获得与合并提交后，以最终等级执行拾取；链式奖励复用同一随机状态。
    private void ApplyPickup(MatchSession session, CardInstance card, SeededRandom? random = null)
    {
        var effects = card.Abilities.Where(ability => ability.Activation == AbilityActivation.PassiveOnPickup)
            .SelectMany(ability => ability.Effects).ToArray();
        if (effects.Length == 0 || session.Player.Hero is null) return;
        var level = card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level);
        var faction = session.Player.Hero.Attributes.Identity.FactionKey;
        random ??= new SeededRandom(session.Random.State);
        foreach (var effect in effects)
        {
            var candidates = _cardDefinitions.Where(definition => definition.SupportsLevel(level) && (effect switch
            {
                GrantRandomOtherHeroSourceLevelCardEffectDefinition other => definition.Attributes.Identity.FactionKey != faction
                    && (definition.Attributes.Identity.Size == other.Size || definition.Attributes.Identity.Size == other.AdditionalSize),
                GrantRandomTaggedSourceLevelCardEffectDefinition tagged => definition.Attributes.Identity.Size == tagged.Size
                    && definition.Tags.Contains(tagged.RequiredTag),
                _ => false,
            })).ToArray();
            if (candidates.Length == 0) continue;
            var reward = candidates[random.NextInt(0, candidates.Length)];
            session.Random.State = random.State;
            if (_boardService is null || AcquireAndPlace(session, reward, level, CardAcquisitionSource.Reward, random).IsFailure)
                session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card,
                    reward.Attributes.Identity.Key, reward.Attributes.Identity.DisplayName, level));
        }
    }

    private Result CheckPlacement(MatchSession session, CardDefinition definition, MergePlan plan)
    {
        var valueCheck = CheckMergeValue(session, definition, plan);
        if (valueCheck.IsFailure) return valueCheck;
        if (_boardService is null || !CanApplyMergePlan(session, plan))
            return Result.Fail(new Failure(MergeBoardUnavailable, "未配置棋盘操作，无法完成交易。"));
        if (plan.TargetId is { } id && session.Board.Contains(id)) return Result.Success();
        return _boardService.FindFirstAvailableTarget(session, definition.Attributes.Identity.OccupiedSlots, plan.ConsumedIds) is null
            ? Result.Fail(new Failure(BoardFull, "双棋盘空间不足。")) : Result.Success();
    }

    // 只读返回出售回补额；移除之前校验奖励依赖、现值与金额溢出。
    public Result<int> CheckSale(MatchSession session, EntityId cardId, bool fromBoard = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        var card = session.Player.Inventory.Find(cardId);
        if (card is null)
        {
            return Result<int>.Fail(new Failure(CardNotOwned, "The card is not in the player's inventory."));
        }

        if (!fromBoard && session.Board.Contains(cardId))
        {
            return Result<int>.Fail(new Failure(CardOnBoard, "Move the card off the board before selling it."));
        }

        if (fromBoard && (_boardService is null || !session.Board.Contains(cardId)))
            return Result<int>.Fail(new Failure(CardOnBoard, "该卡牌不在可出售的棋盘上。"));

        if (card.OnSellReward is RandomTaggedCardOnSellDefinition && (_boardService is null || _cardDefinitions.Count == 0))
        {
            return Result<int>.Fail(new Failure(
                SaleRewardUnavailable,
                "This card requires configured card definitions and board placement to resolve its sale reward."));
        }

        var currentValue = card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
        if (currentValue < 0)
        {
            return Result<int>.Fail(new Failure(InvalidValue, "A card with negative value cannot be sold."));
        }

        try
        {
            _ = checked(session.Player.Wealth + currentValue);
        }
        catch (OverflowException)
        {
            return Result<int>.Fail(new Failure(InvalidValue, "Selling this card would overflow player wealth."));
        }

        if (card.OnSellReward is IncreaseHeroCombatAttributeOnSellDefinition heroBonus)
        {
            if (session.Player.Hero is not { } hero)
                return Result<int>.Fail(new Failure(SaleRewardUnavailable, "A hero is required for this sale reward."));
            if ((long)hero.Attributes.BaseCombat.GetFinalValue(heroBonus.AttributeKey) + heroBonus.Amount > int.MaxValue)
                return Result<int>.Fail(new Failure(InvalidValue, "Sale hero attribute bonus would overflow."));
        }

        if (card.OnSellReward is GrantValuePercentExperienceOnSellDefinition experienceReward)
        {
            if (session.Player.Hero is null)
                return Result<int>.Fail(new Failure(SaleRewardUnavailable, "A hero is required for this sale reward."));
            try { _ = session.Player.PreviewExperienceGain(checked((int)((long)currentValue * experienceReward.Percent / 100))); }
            catch (OverflowException)
            { return Result<int>.Fail(new Failure(InvalidValue, "Sale experience or hero level growth would overflow.")); }
        }

        if (card.OnSellReward is IncreaseLeftmostBurnCardOnSellDefinition burnBonus
            && FindLeftmostBurnCard(session, cardId) is { } burnTarget)
        {
            var pending = burnTarget.SaleAttributeBonuses
                .Where(bonus => bonus.AttributeKey == GameAttributeKeys.BurnBonus && card.Tags.Contains(bonus.RequiredTag))
                .Sum(bonus => (long)bonus.Amount) + burnBonus.Amount;
            var finalBonus = (long)burnTarget.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.BurnBonus) + pending;
            long PendingValue(StringName key) => (long)burnTarget.Attributes.BaseCombat.GetFinalValue(key)
                + burnTarget.SaleAttributeBonuses.Where(bonus => bonus.AttributeKey == key && card.Tags.Contains(bonus.RequiredTag))
                    .Sum(bonus => (long)bonus.Amount);
            if (finalBonus > int.MaxValue || CardAbilityComposer.Compose(burnTarget).SelectMany(ability => ability.Effects)
                .Where(BurnEffectRules.AppliesBurn).Any(effect => BurnEffectRules.BaseAmount(effect, PendingValue) + finalBonus > int.MaxValue))
                return Result<int>.Fail(new Failure(InvalidValue, "Sale burn amount bonus would overflow."));
        }

        if (card.OnSellReward is IncreaseLeftmostPoisonCardOnSellDefinition poisonBonus
            && FindLeftmostPoisonCard(session, cardId) is { } poisonTarget)
        {
            var pending = poisonTarget.SaleAttributeBonuses
                .Where(bonus => bonus.AttributeKey == GameAttributeKeys.PoisonBonus && card.Tags.Contains(bonus.RequiredTag))
                .Sum(bonus => (long)bonus.Amount) + poisonBonus.Amount;
            var finalBonus = (long)poisonTarget.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.PoisonBonus) + pending;
            long PendingValue(StringName key) => (long)poisonTarget.Attributes.BaseCombat.GetFinalValue(key)
                + poisonTarget.SaleAttributeBonuses.Where(bonus => bonus.AttributeKey == key && card.Tags.Contains(bonus.RequiredTag))
                    .Sum(bonus => (long)bonus.Amount);
            if (finalBonus > int.MaxValue || CardAbilityComposer.Compose(poisonTarget).SelectMany(ability => ability.Effects)
                .Where(PoisonEffectRules.AppliesPoison).Any(effect => PoisonEffectRules.BaseAmount(effect, PendingValue) + finalBonus > int.MaxValue))
                return Result<int>.Fail(new Failure(InvalidValue, "Sale poison amount bonus would overflow."));
        }

        if (card.OnSellReward is IncreaseLeftmostHasteCardOnSellDefinition hasteBonus
            && FindLeftmostHasteCard(session, cardId) is { } hasteTarget)
        {
            var pendingBonus = hasteTarget.SaleAttributeBonuses
                .Where(bonus => bonus.AttributeKey == GameAttributeKeys.HasteDurationBonus && card.Tags.Contains(bonus.RequiredTag))
                .Sum(bonus => (long)bonus.Amount) + hasteBonus.AmountTicks;
            var finalBonus = (long)hasteTarget.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HasteDurationBonus) + pendingBonus;
            if (finalBonus > int.MaxValue || CardAbilityComposer.Compose(hasteTarget).SelectMany(ability => ability.Effects)
                .Any(effect => HasteEffectRules.MaximumDurationTicks(effect, finalBonus) > int.MaxValue))
                return Result<int>.Fail(new Failure(InvalidValue, "Sale haste duration bonus would overflow."));
        }

        if (card.OnSellReward is IncreaseLeftmostAttackCardOnSellDefinition attackBonus
            && FindLeftmostAttackCard(session, cardId) is { } attackTarget)
        {
            var pendingBonus = attackTarget.SaleAttributeBonuses
                .Where(bonus => bonus.AttributeKey == GameAttributeKeys.AttackDamage && card.Tags.Contains(bonus.RequiredTag))
                .Sum(bonus => (long)bonus.Amount) + attackBonus.Amount;
            if ((long)attackTarget.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) + pendingBonus > int.MaxValue)
                return Result<int>.Fail(new Failure(InvalidValue, "Sale attack bonus would overflow."));
        }

        if (card.OnSellReward is IncreaseLeftmostHealingCardOnSellDefinition healingBonus
            && FindLeftmostHealingCard(session, cardId) is { } healingTarget)
        {
            var pendingBonus = healingTarget.SaleAttributeBonuses
                .Where(bonus => bonus.AttributeKey == GameAttributeKeys.HealingBonus && card.Tags.Contains(bonus.RequiredTag))
                .Sum(bonus => (long)bonus.Amount) + healingBonus.Amount;
            var finalBonus = (long)healingTarget.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HealingBonus) + pendingBonus;
            if (finalBonus > int.MaxValue || CardAbilityComposer.Compose(healingTarget).SelectMany(ability => ability.Effects)
                .OfType<HealEffectDefinition>().Any(effect => effect.Amount + finalBonus > int.MaxValue))
                return Result<int>.Fail(new Failure(InvalidValue, "Sale healing bonus would overflow."));
        }

        foreach (var source in session.Player.Inventory.Cards)
        {
            if (source.Id == cardId || !session.Board.Contains(source.Id)) continue;
            foreach (var bonuses in source.SaleAttributeBonuses.Where(bonus => bonus.Matches(card.Tags))
                .GroupBy(bonus => bonus.AttributeKey))
                if ((long)(bonuses.Key == GameAttributeKeys.Value ? source.Attributes.Persistent : source.Attributes.BaseCombat)
                    .GetFinalValue(bonuses.Key) + bonuses.Sum(bonus => bonus.CalculateAmount(currentValue)) > int.MaxValue)
                    return Result<int>.Fail(new Failure(InvalidValue, "Sale attribute bonus would overflow."));
        }
        return Result<int>.Success(currentValue);
    }

    // 保留游离库存出售入口供现有应用调用者使用。
    public Result<int> SellCard(MatchSession session, EntityId cardId) => Sell(session, cardId, false);

    // 从棋盘出售，一次完成移除、库存删除、现值回补和既有出售奖励。
    public Result<int> SellFromBoard(MatchSession session, EntityId cardId) => Sell(session, cardId, true);

    private Result<int> Sell(MatchSession session, EntityId cardId, bool fromBoard)
    {
        var check = CheckSale(session, cardId, fromBoard);
        if (check.IsFailure) return check;
        var card = session.Player.Inventory.Find(cardId)!;
        if (fromBoard) _boardService!.RemoveFromBoard(session, cardId);
        session.Player.Inventory.Remove(cardId);
        session.Player.AddWealth(check.Value);
        ApplySaleAttributeBonuses(session, card, check.Value);
        new CardQuestService(_boardService).ProcessEvent(session, new CardSoldQuestEvent(Array.AsReadOnly(card.Tags.ToArray())));
        ApplyOnSellReward(session, card);
        return check;
    }

    private static void ApplySaleAttributeBonuses(MatchSession session, CardInstance soldCard, int soldValue)
    {
        foreach (var source in session.Player.Inventory.Cards)
        {
            if (!session.Board.Contains(source.Id)) continue;
            foreach (var bonus in source.SaleAttributeBonuses)
                if (bonus.Matches(soldCard.Tags))
                    (bonus.AttributeKey == GameAttributeKeys.Value ? source.Attributes.Persistent : source.Attributes.BaseCombat)
                        .ApplyModifier(new StatModifier(ModifierId.New(), source.Id, bonus.AttributeKey,
                            checked((int)bonus.CalculateAmount(soldValue))));
        }
    }

    private static CardInstance? FindLeftmostBurnCard(MatchSession session, EntityId excludedCardId) =>
        session.Board.Battlefield.Placements.OrderBy(placement => placement.Start)
            .Where(placement => placement.CardId != excludedCardId)
            .Select(placement => session.Player.Inventory.Find(placement.CardId))
            .FirstOrDefault(card => card is not null && CardAbilityComposer.Compose(card)
                .Any(ability => ability.Effects.Any(BurnEffectRules.AppliesBurn)));

    private static CardInstance? FindLeftmostPoisonCard(MatchSession session, EntityId excludedCardId) =>
        session.Board.Battlefield.Placements.OrderBy(placement => placement.Start)
            .Where(placement => placement.CardId != excludedCardId)
            .Select(placement => session.Player.Inventory.Find(placement.CardId))
            .FirstOrDefault(card => card is not null && CardAbilityComposer.Compose(card)
                .Any(ability => ability.Effects.Any(PoisonEffectRules.AppliesPoison)));

    private static CardInstance? FindLeftmostHasteCard(MatchSession session, EntityId excludedCardId) =>
        session.Board.Battlefield.Placements.OrderBy(placement => placement.Start)
            .Where(placement => placement.CardId != excludedCardId)
            .Select(placement => session.Player.Inventory.Find(placement.CardId))
            .FirstOrDefault(card => card is not null && CardAbilityComposer.Compose(card)
                .Any(ability => ability.Effects.Any(HasteEffectRules.AppliesHaste)));

    private static CardInstance? FindLeftmostAttackCard(MatchSession session, EntityId excludedCardId) =>
        session.Board.Battlefield.Placements.OrderBy(placement => placement.Start)
            .Where(placement => placement.CardId != excludedCardId)
            .Select(placement => session.Player.Inventory.Find(placement.CardId))
            .FirstOrDefault(card => card is not null && CardAbilityComposer.Compose(card)
                .Any(ability => ability.Effects.Any(effect => effect is
                    DamageEffectDefinition or AttributeDamageEffectDefinition or DestroyAlliedCardsAndDamageEffectDefinition or MaxHealthPercentDamageEffectDefinition
                    or HeroManaDamageEffectDefinition or SkillCountDamageEffectDefinition or SourceHeroLevelScaledDamageEffectDefinition or SourceHeroHealthScaledAttributeDamageEffectDefinition
                    or SourceHeroArmorDamageEffectDefinition)));

    private static CardInstance? FindLeftmostHealingCard(MatchSession session, EntityId excludedCardId) =>
        session.Board.Battlefield.Placements.OrderBy(placement => placement.Start)
            .Where(placement => placement.CardId != excludedCardId)
            .Select(placement => session.Player.Inventory.Find(placement.CardId))
            .FirstOrDefault(card => card is not null && CardAbilityComposer.Compose(card)
                .Any(ability => ability.Effects.Any(effect => effect is HealEffectDefinition or HealMaxHealthPercentEffectDefinition)));

    private void ApplyOnSellReward(MatchSession session, CardInstance soldCard)
    {
        if (soldCard.OnSellReward is GrantValuePercentExperienceOnSellDefinition experienceReward)
        {
            var value = soldCard.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            HeroExperienceService.Grant(session, checked((int)((long)value * experienceReward.Percent / 100)));
            _boardService?.RefreshBonuses(session);
            return;
        }
        if (soldCard.OnSellReward is IncreaseLeftmostBurnCardOnSellDefinition burnBonus)
        {
            FindLeftmostBurnCard(session, soldCard.Id)?.Attributes.BaseCombat.ApplyModifier(new StatModifier(
                ModifierId.New(), soldCard.Id, GameAttributeKeys.BurnBonus, burnBonus.Amount));
            return;
        }
        if (soldCard.OnSellReward is IncreaseLeftmostPoisonCardOnSellDefinition poisonBonus)
        {
            FindLeftmostPoisonCard(session, soldCard.Id)?.Attributes.BaseCombat.ApplyModifier(new StatModifier(
                ModifierId.New(), soldCard.Id, GameAttributeKeys.PoisonBonus, poisonBonus.Amount));
            return;
        }
        if (soldCard.OnSellReward is IncreaseLeftmostHasteCardOnSellDefinition hasteBonus)
        {
            FindLeftmostHasteCard(session, soldCard.Id)?.Attributes.BaseCombat.ApplyModifier(new StatModifier(
                ModifierId.New(), soldCard.Id, GameAttributeKeys.HasteDurationBonus, hasteBonus.AmountTicks));
            return;
        }
        if (soldCard.OnSellReward is IncreaseLeftmostAttackCardOnSellDefinition attackBonus)
        {
            FindLeftmostAttackCard(session, soldCard.Id)?.Attributes.BaseCombat.ApplyModifier(new StatModifier(
                ModifierId.New(), soldCard.Id, GameAttributeKeys.AttackDamage, attackBonus.Amount));
            return;
        }
        if (soldCard.OnSellReward is IncreaseLeftmostHealingCardOnSellDefinition healingBonus)
        {
            FindLeftmostHealingCard(session, soldCard.Id)?.Attributes.BaseCombat.ApplyModifier(new StatModifier(
                ModifierId.New(), soldCard.Id, GameAttributeKeys.HealingBonus, healingBonus.Amount));
            return;
        }
        if (soldCard.OnSellReward is IncreaseHeroCombatAttributeOnSellDefinition heroBonus)
        {
            session.Player.Hero!.Attributes.BaseCombat.ApplyModifier(new StatModifier(
                ModifierId.New(), soldCard.Id, heroBonus.AttributeKey, heroBonus.Amount));
            return;
        }
        if (soldCard.OnSellReward is ReduceLeftmostElementCardCooldownOnSellDefinition reduction)
        {
            var target = session.Board.Battlefield.Placements.OrderBy(placement => placement.Start)
                .Select(placement => session.Player.Inventory.Find(placement.CardId))
                .FirstOrDefault(card => card is not null && card.Attributes.Identity.ElementKeys.Contains(reduction.ElementKey));
            target?.ReduceCooldown(reduction.Percent);
            return;
        }
        if (soldCard.OnSellReward is not RandomTaggedCardOnSellDefinition reward
            || _boardService is null)
        {
            return;
        }

        var soldLevel = soldCard.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level);
        var candidates = new List<CardDefinition>();
        foreach (var definition in _cardDefinitions)
        {
            if (!definition.Tags.Contains(reward.RequiredTag)) continue;
            if (reward.RequiredSize is not null
                && definition.Attributes.Identity.Size != reward.RequiredSize.Value) continue;
            if (reward.SameLevel && !definition.SupportsLevel(soldLevel)) continue;
            candidates.Add(definition);
        }
        if (candidates.Count == 0) return;

        var random = new SeededRandom(session.Random.State);
        for (var rewardIndex = 0; rewardIndex < reward.Count; rewardIndex++)
        {
            var selected = candidates[random.NextInt(0, candidates.Count)];
            var level = reward.SameLevel ? soldLevel : selected.InitialLevel;
            // 沿用空间不足时跳过该件出售奖励的既有规则；成功仍走完整获得入口。
            _ = AcquireAndPlace(session, selected, level, CardAcquisitionSource.Reward);
        }
        session.Random.State = random.State;
    }

    // 判断本次获得是否可以直接合并而无需新棋盘位置。
    public EntityId? FindMergeTarget(MatchSession session, CardDefinition definition, int level) =>
        BuildMergePlan(session, definition, level).TargetId;

    private CardAcquisitionResult AcquireOrMerge(
        MatchSession session,
        CardDefinition definition,
        int level,
        MergePlan plan)
    {
        if (plan.TargetId is null)
        {
            var created = CreateAcquiredCard(definition, level);
            session.Player.Inventory.Add(created);
            ApplyAcquisitionBonuses(session, definition, plan);
            new CardQuestService(_boardService).ProcessEvent(session,
                new CardAcquiredQuestEvent(definition.Attributes.Identity.ElementKeys, created.Id));
            return new CardAcquisitionResult(created, true, level, level);
        }

        foreach (var consumedId in plan.ConsumedIds)
        {
            if (session.Board.Contains(consumedId))
            {
                var removed = _boardService!.RemoveFromBoard(session, consumedId);
                if (removed.IsFailure) throw new InvalidOperationException(removed.Failure!.Message);
            }
            _ = session.Player.Inventory.Remove(consumedId);
        }
        var target = session.Player.Inventory.Find(plan.TargetId.Value)
            ?? throw new InvalidOperationException("Merge target is not owned by the player.");
        var previousLevel = target.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level);
        var value = checked(target.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value)
            + CardValueCalculator.CalculateMergeCompensation(definition, previousLevel, plan.FinalLevel));
        CardLevelUpgradeService.Upgrade(session, target, definition, plan.FinalLevel, _entityFactory);
        target.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, value);
        ApplyAcquisitionBonuses(session, definition, plan);
        new CardQuestService(_boardService).ProcessEvent(session,
            new CardAcquiredQuestEvent(definition.Attributes.Identity.ElementKeys));
        return new CardAcquisitionResult(target, false, previousLevel, plan.FinalLevel);
    }

    private bool CanApplyMergePlan(MatchSession session, MergePlan plan) =>
        _boardService is not null || plan.ConsumedIds.All(id => !session.Board.Contains(id));

    private static Result CheckMergeValue(MatchSession session, CardDefinition definition, MergePlan plan)
    {
        try
        {
            foreach (var (card, key, amount) in AcquisitionBonuses(session, definition, plan))
            {
                _ = checked(card.Attributes.Persistent.GetBaseValue(key) + amount);
                _ = checked(card.Attributes.Persistent.GetFinalValue(key) + amount);
            }
            if (plan.TargetId is not { } id) return Result.Success();
            var target = session.Player.Inventory.Find(id)!;
            var delta = CardValueCalculator.CalculateMergeCompensation(definition,
                target.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level), plan.FinalLevel);
            if (checked(target.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Value) + delta) < 0
                || checked(target.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) + delta) < 0)
                return Result.Fail(new Failure(InvalidValue, "合成后的卡牌价值不能为负。"));
        }
        catch (OverflowException)
        {
            return Result.Fail(new Failure(InvalidValue, "获得卡牌后的价值或成长属性超出范围。"));
        }
        return Result.Success();
    }

    // 从既有战场、备战来源收集本次成功获得的贡献；合并材料和获得者自身不触发。
    private static IEnumerable<(CardInstance Card, StringName Key, int Amount)> AcquisitionBonuses(
        MatchSession session, CardDefinition definition, MergePlan plan)
    {
        var tags = plan.TargetId is { } id ? session.Player.Inventory.Find(id)!.Tags : definition.Tags;
        foreach (var card in session.Player.Inventory.Cards)
        {
            if (!session.Board.Contains(card.Id) || card.Id == plan.TargetId || plan.ConsumedIds.Contains(card.Id)) continue;
            foreach (var group in card.AcquisitionAttributeBonuses.Where(bonus => tags.Contains(bonus.RequiredTag))
                .GroupBy(bonus => bonus.AttributeKey))
                yield return (card, group.Key, checked(group.Sum(bonus => bonus.Amount)));
        }
    }

    private static void ApplyAcquisitionBonuses(MatchSession session, CardDefinition definition, MergePlan plan)
    {
        foreach (var (card, key, amount) in AcquisitionBonuses(session, definition, plan))
            card.Attributes.Persistent.SetBaseValue(key, checked(card.Attributes.Persistent.GetBaseValue(key) + amount));
    }

    private MergePlan BuildMergePlan(MatchSession session, CardDefinition definition, int level)
    {
        var candidates = session.Player.Inventory.Cards
            .Select(card => new MergeCandidate(
                card.Id,
                card.Attributes.Identity.Key,
                card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level)))
            .ToArray();
        var targetId = definition.SupportsLevel(level + 1)
            ? MergeDecision.FindTarget(definition.Attributes.Identity.Key, level, candidates)
            : null;
        if (targetId is null) return new MergePlan(null, level, Array.Empty<EntityId>());
        var excluded = new HashSet<EntityId> { targetId.Value };
        var consumed = new List<EntityId>();
        var currentLevel = level + 1;
        while (currentLevel < MergeDecision.MaximumMergeLevel && definition.SupportsLevel(currentLevel + 1))
        {
            var next = MergeDecision.FindTarget(
                definition.Attributes.Identity.Key,
                currentLevel,
                candidates,
                excluded);
            if (next is null) break;
            // 每一步由原有高等级卡吸收低等级结果，保留其完整实例状态。
            consumed.Add(targetId.Value);
            targetId = next.Value;
            excluded.Add(next.Value);
            currentLevel++;
        }
        return new MergePlan(targetId, currentLevel, consumed.AsReadOnly());
    }

    private sealed record MergePlan(EntityId? TargetId, int FinalLevel, IReadOnlyList<EntityId> ConsumedIds);

    private CardInstance CreateAcquiredCard(CardDefinition definition, int level)
    {
        var initialValue = CardValueCalculator.CalculateInitialValue(definition, level);
        var card = _entityFactory.CreateCard(definition, level);
        var valueBonus = definition.GetLevel(level)?.AcquiredValueBonus ?? 0;
        card.Attributes.Persistent.SetBaseValue(
            GameAttributeKeys.Value,
            checked(CardValueCalculator.CalculateAcquiredValue(initialValue) + valueBonus));
        return card;
    }
}
