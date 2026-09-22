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

/// <summary>Executes atomic card acquisition, purchase, and sale operations.</summary>
public sealed class CardEconomyService
{
    private static readonly StringName InsufficientWealth = new("economy.insufficient_wealth");
    private static readonly StringName CardNotOwned = new("economy.card_not_owned");
    private static readonly StringName CardOnBoard = new("economy.card_on_board");
    private static readonly StringName InvalidValue = new("economy.invalid_value");
    private static readonly StringName OfferSold = new("economy.offer_sold");
    private static readonly StringName SaleRewardUnavailable = new("economy.sale_reward_unavailable");

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

    public Result<CardInstance> AcquireCard(
        MatchSession session,
        CardDefinition definition,
        int level,
        CardAcquisitionSource source)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);
        _ = source;

        var card = CreateAcquiredCard(definition, level);
        session.Player.Inventory.Add(card);
        return Result<CardInstance>.Success(card);
    }

    public Result<CardInstance> BuyCard(MatchSession session, ShopOffer offer)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(offer);

        if (offer.IsSold)
        {
            return Result<CardInstance>.Fail(new Failure(OfferSold, "This shop offer has already been purchased."));
        }

        if (session.Player.Wealth < offer.Price)
        {
            return Result<CardInstance>.Fail(new Failure(InsufficientWealth, "Not enough wealth to buy this card."));
        }

        var card = CreateAcquiredCard(offer.Definition, offer.Level);
        if (!session.Player.TrySpendWealth(offer.Price))
        {
            return Result<CardInstance>.Fail(new Failure(InsufficientWealth, "Not enough wealth to buy this card."));
        }

        session.Player.Inventory.Add(card);
        offer.MarkSold();
        return Result<CardInstance>.Success(card);
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
            if (reward.SameLevel && !definition.SupportsLevel(soldLevel)) continue;
            candidates.Add(definition);
        }
        if (candidates.Count == 0) return;

        var random = new SeededRandom(session.Random.State);
        var selected = candidates[random.NextInt(0, candidates.Count)];
        session.Random.State = random.State;
        var level = reward.SameLevel ? soldLevel : selected.InitialLevel;
        var target = _boardService.FindFirstAvailableTarget(
            session,
            selected.Attributes.Identity.OccupiedSlots);
        if (target is null) return;

        var acquired = CreateAcquiredCard(selected, level);
        session.Player.Inventory.Add(acquired);
        var placement = _boardService.PlaceCard(session, acquired.Id, target.Zone, target.Start);
        if (placement.IsFailure)
            _ = session.Player.Inventory.Remove(acquired.Id);
    }

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
