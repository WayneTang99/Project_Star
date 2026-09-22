using System;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Combat;

/// <summary>Copies match state into an immutable battle input.</summary>
public sealed class BattleSetupFactory
{
    public BattleSetup Create(
        MatchSession playerSession,
        MatchSession opponentSession,
        ulong seed,
        BattleTick timeout,
        BattleTick? eclipseTime = null)
    {
        ArgumentNullException.ThrowIfNull(playerSession);
        ArgumentNullException.ThrowIfNull(opponentSession);
        return new BattleSetup(
            CreateSide(playerSession),
            CreateSide(opponentSession),
            seed,
            timeout,
            eclipseTime ?? new BattleTick(300));
    }

    private static BattleSideSetup CreateSide(MatchSession session)
    {
        var hero = session.Player.Hero
            ?? throw new InvalidOperationException("A hero must be selected before battle.");
        var maxHealth = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
        var armor = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor);
        var maxMana = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxMana);
        var mana = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Mana);
        var manaRegen = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.ManaRegen);
        var healthRegen = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HealthRegen);
        var burn = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn);
        var poison = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Poison);
        if (maxHealth < 1 || armor < 0 || maxMana < 0 || mana < 0 || mana > maxMana
            || manaRegen < 0 || healthRegen < 0 || burn < 0 || poison < 0)
        {
            throw new InvalidOperationException("Hero battle attributes are invalid.");
        }

        var cards = new List<CardBattleSetup>();
        foreach (var placement in session.Board.Battlefield.Placements)
        {
            var card = session.Player.Inventory.Find(placement.CardId)
                ?? throw new InvalidOperationException($"Battlefield card '{placement.CardId}' is not owned by the player.");
            var damage = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage);
            var cooldown = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks);
            if (damage < 0 || cooldown < 1)
            {
                throw new InvalidOperationException($"Card '{placement.CardId}' has invalid battle attributes.");
            }

            cards.Add(new CardBattleSetup(
                card.Id,
                placement.Start,
                damage,
                cooldown,
                card.Abilities,
                UseLegacyAttack: false));
        }

        foreach (var placement in session.Board.Bench.Placements)
        {
            var card = session.Player.Inventory.Find(placement.CardId)
                ?? throw new InvalidOperationException($"Bench card '{placement.CardId}' is not owned by the player.");
            cards.Add(new CardBattleSetup(
                card.Id,
                placement.Start,
                0,
                1,
                card.Abilities,
                IsOnBench: true,
                UseLegacyAttack: false));
        }

        return new BattleSideSetup(
            new HeroBattleSetup(hero.Id, maxHealth, armor, maxMana, mana, manaRegen, healthRegen, burn, poison),
            cards.AsReadOnly());
    }
}
