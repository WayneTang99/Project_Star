using System;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Factories;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

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

    private readonly EntityFactory _entityFactory;

    public CardEconomyService(EntityFactory entityFactory)
    {
        _entityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
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
        return Result<int>.Success(currentValue);
    }

    private CardInstance CreateAcquiredCard(CardDefinition definition, int level)
    {
        var initialValue = CardValueCalculator.CalculateInitialValue(definition, level);
        var card = _entityFactory.CreateCard(definition, level);
        card.Attributes.Persistent.SetBaseValue(
            GameAttributeKeys.Value,
            CardValueCalculator.CalculateAcquiredValue(initialValue));
        return card;
    }
}
