using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Application.Factories;
using Project_Star.Domain.Common;
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

/// <summary>Executes atomic card acquisition, purchase, and sale operations.</summary>
public sealed class CardEconomyService
{
    private static readonly StringName InsufficientWealth = new("economy.insufficient_wealth");
    private static readonly StringName CardNotOwned = new("economy.card_not_owned");
    private static readonly StringName CardOnBoard = new("economy.card_on_board");
    private static readonly StringName InvalidValue = new("economy.invalid_value");
    private static readonly StringName OfferSold = new("economy.offer_sold");
    private static readonly StringName SaleRewardUnavailable = new("economy.sale_reward_unavailable");
    private static readonly StringName MergeBoardUnavailable = new("economy.merge_board_unavailable");

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
        return Result<CardAcquisitionResult>.Success(AcquireOrMerge(session, definition, level, plan));
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

        if (!session.Player.TrySpendWealth(offer.Price))
        {
            return Result<CardAcquisitionResult>.Fail(new Failure(InsufficientWealth, "Not enough wealth to buy this card."));
        }

        var result = AcquireOrMerge(session, offer.Definition, offer.Level, plan);
        offer.MarkSold();
        return Result<CardAcquisitionResult>.Success(result);
    }

    public Result<int> SellCard(MatchSession session, EntityId cardId)
    {
        ArgumentNullException.ThrowIfNull(session);
        var card = session.Player.Inventory.Find(cardId);
        if (card is null)
        {
            return Result<int>.Fail(new Failure(CardNotOwned, "The card is not in the player's inventory."));
        }

        if (session.Board.Contains(cardId))
        {
            return Result<int>.Fail(new Failure(CardOnBoard, "Move the card off the board before selling it."));
        }

        if (card.OnSellReward is not null && (_boardService is null || _cardDefinitions.Count == 0))
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

        if (!session.Player.Inventory.Remove(cardId))
        {
            return Result<int>.Fail(new Failure(CardNotOwned, "The card is not in the player's inventory."));
        }

        session.Player.AddWealth(currentValue);
        ApplyOnSellReward(session, card);
        return Result<int>.Success(currentValue);
    }

    private void ApplyOnSellReward(MatchSession session, CardInstance soldCard)
    {
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
            var mergeTarget = FindMergeTarget(session, selected, level);
            var target = mergeTarget is null
                ? _boardService.FindFirstAvailableTarget(session, selected.Attributes.Identity.OccupiedSlots)
                : null;
            if (mergeTarget is null && target is null) continue;
            var acquired = AcquireOrMerge(session, selected, level, BuildMergePlan(session, selected, level));
            if (!acquired.WasCreated) continue;
            var placement = _boardService.PlaceCard(session, acquired.Card.Id, target!.Zone, target.Start);
            if (placement.IsFailure) _ = session.Player.Inventory.Remove(acquired.Card.Id);
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
        _entityFactory.ApplyCardLevel(target, definition, plan.FinalLevel);
        return new CardAcquisitionResult(target, false, level, plan.FinalLevel);
    }

    private bool CanApplyMergePlan(MatchSession session, MergePlan plan) =>
        _boardService is not null || plan.ConsumedIds.All(id => !session.Board.Contains(id));

    private MergePlan BuildMergePlan(MatchSession session, CardDefinition definition, int level)
    {
        var candidates = session.Player.Inventory.Cards
            .Select(card => new CardMergeCandidate(
                card.Id,
                card.Attributes.Identity.Key,
                card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level)))
            .ToArray();
        var targetId = definition.SupportsLevel(level + 1)
            ? CardMergeDecision.FindTarget(definition.Attributes.Identity.Key, level, candidates)
            : null;
        if (targetId is null) return new MergePlan(null, level, Array.Empty<EntityId>());
        var excluded = new HashSet<EntityId> { targetId.Value };
        var consumed = new List<EntityId>();
        var currentLevel = level + 1;
        while (currentLevel < CardMergeDecision.MaximumMergeLevel && definition.SupportsLevel(currentLevel + 1))
        {
            var next = CardMergeDecision.FindTarget(
                definition.Attributes.Identity.Key,
                currentLevel,
                candidates,
                excluded);
            if (next is null) break;
            consumed.Add(next.Value);
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
