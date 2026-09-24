using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Encounters;

public sealed record EncounterAttributeChange(StringName AttributeKey, int Amount, int CurrentValue);
public sealed record EncounterCardAttributeChange(StringName AttributeKey, int Amount, int CardCount);

public sealed record EncounterOptionResult(
    StringName OptionKey,
    IReadOnlyList<EncounterAttributeChange> Changes,
    int WealthGained = 0,
    CardInstance? GrantedCard = null,
    bool CardRewardSkipped = false,
    int WealthSpent = 0,
    int PendingBattleMaxHealthBonus = 0,
    IReadOnlyList<EncounterCardAttributeChange>? CardChanges = null);

// 单次可选项遭遇展示状态（应用层遭遇模块）。
public sealed class EncounterOptionSet
{
    internal EncounterOptionSet(ChoiceEncounterDefinition encounter, IReadOnlyList<EncounterOptionDefinition> options)
    {
        Encounter = encounter;
        Options = options;
    }

    public ChoiceEncounterDefinition Encounter { get; }
    public IReadOnlyList<EncounterOptionDefinition> Options { get; }
    public bool IsResolved { get; private set; }
    internal void MarkResolved() => IsResolved = true;
}

// 生成并结算可选项遭遇（应用层遭遇模块）。
public sealed class ResolveEncounterOptionService
{
    private static readonly StringName HeroMissing = new("encounter.hero_missing");
    private static readonly StringName OptionMissing = new("encounter.option_missing");
    private static readonly StringName AlreadyResolved = new("encounter.already_resolved");
    private static readonly StringName RewardUnavailable = new("encounter.reward_unavailable");

    private readonly EntityFactory _factory;
    private readonly BoardService _board;
    private readonly IReadOnlyList<CardDefinition> _cards;

    public ResolveEncounterOptionService(
        EntityFactory factory,
        BoardService board,
        IEnumerable<CardDefinition> cards)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _board = board ?? throw new ArgumentNullException(nameof(board));
        _cards = cards?.OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray()
            ?? throw new ArgumentNullException(nameof(cards));
    }

    // 按选项槽和权重生成本次实际展示的选项。
    public EncounterOptionSet CreateOptionSet(MatchSession session, ChoiceEncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(encounter);
        var random = new SeededRandom(session.Random.State);
        var optionsByKey = encounter.Options.ToDictionary(option => option.Key);
        var selected = new List<EncounterOptionDefinition>(encounter.OptionSlots.Count);
        foreach (var slot in encounter.OptionSlots)
        {
            var totalWeight = slot.Candidates.Sum(candidate => candidate.Weight);
            var roll = random.NextInt(0, totalWeight);
            foreach (var candidate in slot.Candidates)
            {
                if (roll < candidate.Weight)
                {
                    selected.Add(optionsByKey[candidate.OptionKey]);
                    break;
                }
                roll -= candidate.Weight;
            }
        }
        session.Random.State = random.State;
        return new EncounterOptionSet(encounter, selected.AsReadOnly());
    }

    // 执行本次实际展示的指定选项并返回奖励结果。
    public Result<EncounterOptionResult> Resolve(
        MatchSession session,
        EncounterOptionSet optionSet,
        StringName optionKey)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(optionSet);
        if (optionSet.IsResolved)
            return Result<EncounterOptionResult>.Fail(new Failure(AlreadyResolved, "This encounter has already been resolved."));
        var hero = session.Player.Hero;
        if (hero is null)
            return Result<EncounterOptionResult>.Fail(new Failure(HeroMissing, "A hero must be selected first."));
        var selected = optionSet.Options.FirstOrDefault(option => option.Key == optionKey);
        if (selected is null)
            return Result<EncounterOptionResult>.Fail(new Failure(OptionMissing, "The encounter option is not available."));

        var level = hero.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level);
        var changes = new List<EncounterAttributeChange>();
        var cardChanges = new List<EncounterCardAttributeChange>();
        var cardUpdates = new List<(CardInstance Card, StringName AttributeKey, int CurrentValue)>();
        var wealth = 0;
        var wealthSpent = 0;
        CardInstance? grantedCard = null;
        var cardRewardSkipped = false;
        var pendingBattleMaxHealthBonus = 0;
        foreach (var effect in selected.Effects)
        {
            switch (effect)
            {
                case ModifyHeroCombatAttributeByLevelEffectDefinition modifier:
                    var amount = checked(level * modifier.AmountPerLevel);
                    var current = checked(hero.Attributes.BaseCombat.GetBaseValue(modifier.AttributeKey) + amount);
                    changes.Add(new EncounterAttributeChange(modifier.AttributeKey, amount, current));
                    break;
                case GainWealthEncounterOptionEffectDefinition gain:
                    wealth = checked(wealth + gain.Amount);
                    break;
                case GrantRandomFactionCardEncounterOptionEffectDefinition factionCard:
                    var factionCandidates = _cards.Where(card =>
                        card.Attributes.Identity.FactionKey == hero.Attributes.Identity.FactionKey
                        && card.Attributes.Identity.Size == factionCard.Size
                        && card.SupportsLevel(factionCard.Level)).ToArray();
                    var factionReward = GrantRandomCard(session, factionCandidates, factionCard.Level);
                    if (factionReward.IsFailure) return Result<EncounterOptionResult>.Fail(factionReward.Failure!);
                    grantedCard = factionReward.Value;
                    cardRewardSkipped = grantedCard is null;
                    break;
                case GrantRandomTaggedCardEncounterOptionEffectDefinition taggedCard:
                    var taggedCandidates = _cards.Where(card =>
                        card.Tags.Contains(taggedCard.RequiredTag)
                        && card.Attributes.Identity.Size == taggedCard.Size
                        && card.SupportsLevel(taggedCard.Level)).ToArray();
                    var taggedReward = GrantRandomCard(session, taggedCandidates, taggedCard.Level);
                    if (taggedReward.IsFailure) return Result<EncounterOptionResult>.Fail(taggedReward.Failure!);
                    grantedCard = taggedReward.Value;
                    cardRewardSkipped = grantedCard is null;
                    break;
                case BuyRandomOtherFactionCardEncounterOptionEffectDefinition purchase:
                    var purchaseResult = BuyRandomOtherFactionCard(session, purchase, hero.Attributes.Identity.FactionKey);
                    if (purchaseResult.IsFailure) return Result<EncounterOptionResult>.Fail(purchaseResult.Failure!);
                    grantedCard = purchaseResult.Value;
                    wealthSpent = purchase.Cost;
                    break;
                case GrantNextBattleMaxHealthByLevelEncounterOptionEffectDefinition battleHealth:
                    pendingBattleMaxHealthBonus = checked(level * battleHealth.AmountPerLevel);
                    break;
                case ModifyBattlefieldCardAttributeEncounterOptionEffectDefinition battlefieldModifier:
                    var affectedCards = 0;
                    foreach (var placement in session.Board.Battlefield.Placements)
                    {
                        var card = session.Player.Inventory.Find(placement.CardId)
                            ?? throw new InvalidOperationException("A battlefield card must exist in the player's inventory.");
                        var attributes = card.Attributes.BaseCombat;
                        var currentValue = attributes.GetBaseValue(battlefieldModifier.AttributeKey);
                        var abilityUsesAttribute = card.Abilities
                            .SelectMany(ability => ability.Effects)
                            .OfType<SourceHeroArmorDamageEffectDefinition>()
                            .Any(effect => effect.BonusAttributeKey == battlefieldModifier.AttributeKey);
                        if (!attributes.HasBaseValue(battlefieldModifier.AttributeKey)
                            || battlefieldModifier.AttributeKey == GameAttributeKeys.AttackDamage
                                && currentValue <= 0 && !abilityUsesAttribute)
                            continue;
                        var cardCurrent = checked(currentValue + battlefieldModifier.Amount);
                        cardUpdates.Add((card, battlefieldModifier.AttributeKey, cardCurrent));
                        affectedCards++;
                    }
                    cardChanges.Add(new EncounterCardAttributeChange(
                        battlefieldModifier.AttributeKey,
                        battlefieldModifier.Amount,
                        affectedCards));
                    break;
            }
        }

        foreach (var change in changes)
            hero.Attributes.BaseCombat.SetBaseValue(change.AttributeKey, change.CurrentValue);
        foreach (var update in cardUpdates)
            update.Card.Attributes.BaseCombat.SetBaseValue(update.AttributeKey, update.CurrentValue);
        if (wealth > 0) session.Player.AddWealth(wealth);
        if (pendingBattleMaxHealthBonus > 0)
            session.AddPendingBattleMaxHealthBonus(pendingBattleMaxHealthBonus);
        optionSet.MarkResolved();
        return Result<EncounterOptionResult>.Success(
            new EncounterOptionResult(
                selected.Key,
                changes.AsReadOnly(),
                wealth,
                grantedCard,
                cardRewardSkipped,
                wealthSpent,
                pendingBattleMaxHealthBonus,
                cardChanges.AsReadOnly()));
    }

    private Result<CardInstance> BuyRandomOtherFactionCard(
        MatchSession session,
        BuyRandomOtherFactionCardEncounterOptionEffectDefinition purchase,
        StringName currentFaction)
    {
        if (session.Player.Wealth < purchase.Cost)
            return Result<CardInstance>.Fail(new Failure(
                new StringName("economy.insufficient_wealth"), "Not enough wealth to buy this card."));

        var candidates = _cards.Where(card =>
            card.Attributes.Identity.FactionKey != currentFaction
            && card.Attributes.Identity.FactionKey != GameFactions.Neutral
            && card.Attributes.Identity.Size == purchase.Size
            && card.SupportsLevel(purchase.Level)).ToArray();
        if (candidates.Length == 0)
            return Result<CardInstance>.Fail(new Failure(RewardUnavailable, "No other-faction card matches this encounter purchase."));

        var random = new SeededRandom(session.Random.State);
        var selected = candidates[random.NextInt(0, candidates.Length)];
        var economy = new CardEconomyService(_factory, _board);
        var mergeTarget = economy.FindMergeTarget(session, selected, purchase.Level);
        var target = mergeTarget is null
            ? _board.FindFirstAvailableTarget(session, selected.Attributes.Identity.OccupiedSlots)
            : null;
        if (mergeTarget is null && target is null)
            return Result<CardInstance>.Fail(new Failure(RewardUnavailable, "There is no room for the purchased card."));
        if (!session.Player.TrySpendWealth(purchase.Cost))
            return Result<CardInstance>.Fail(new Failure(
                new StringName("economy.insufficient_wealth"), "Not enough wealth to buy this card."));

        var acquired = economy.AcquireCard(session, selected, purchase.Level, CardAcquisitionSource.Purchase);
        if (acquired.IsFailure) throw new InvalidOperationException(acquired.Failure!.Message);
        if (acquired.Value!.WasCreated)
        {
            var placement = _board.PlaceCard(session, acquired.Value.Card.Id, target!.Zone, target.Start);
            if (placement.IsFailure) throw new InvalidOperationException(placement.Failure!.Message);
        }
        session.Random.State = random.State;
        return Result<CardInstance>.Success(acquired.Value.Card);
    }

    private Result<CardInstance?> GrantRandomCard(
        MatchSession session,
        IReadOnlyList<CardDefinition> candidates,
        int level)
    {
        if (candidates.Count == 0)
            return Result<CardInstance?>.Fail(new Failure(RewardUnavailable, "No card matches this encounter reward."));
        var random = new SeededRandom(session.Random.State);
        var selected = candidates[random.NextInt(0, candidates.Count)];
        session.Random.State = random.State;
        var economy = new CardEconomyService(_factory, _board);
        var mergeTarget = economy.FindMergeTarget(session, selected, level);
        var target = mergeTarget is null
            ? _board.FindFirstAvailableTarget(session, selected.Attributes.Identity.OccupiedSlots)
            : null;
        if (mergeTarget is null && target is null) return Result<CardInstance?>.Success(null);
        var acquired = economy.AcquireCard(
            session,
            selected,
            level,
            CardAcquisitionSource.Reward);
        if (acquired.IsFailure) return Result<CardInstance?>.Fail(acquired.Failure!);
        if (acquired.Value!.WasCreated)
        {
            var placement = _board.PlaceCard(session, acquired.Value.Card.Id, target!.Zone, target.Start);
            if (placement.IsFailure) throw new InvalidOperationException(placement.Failure!.Message);
        }
        return Result<CardInstance?>.Success(acquired.Value.Card);
    }
}
