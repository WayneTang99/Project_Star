using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Application.Economy;
using Project_Star.Presentation.CardFace;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

internal static class PaladinActivationChecks
{
    internal static bool LuksaExtraActivation()
    {
        var definition = new ArchbishopLuksaCardDefinition();
        if (definition.InitialLevel != 5 || definition.SupportsLevel(4) || !definition.Tags.Contains(GameTags.Human)) return false;
        var rules = string.Join("\n", CardDisplayAdapter.Rules(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition))));
        if (!rules.Contains("亡语") || rules.Contains("回响") || CardKeywords.DisplayName(CardKeywords.Deathrattle) != "亡语") return false;
        foreach (var reverse in new[] { false, true })
        {
            var root = Card(definition, 5, 0);
            var other = Card(definition, 5, 2) with { Abilities = [Trigger(300)] };
            var light = Receiver(4, GameElements.Light) with { Multicast = 1 };
            var dark = Receiver(6, GameElements.Dark);
            var bench = Receiver(0, GameElements.Light) with { IsOnBench = true };
            var enemy = Receiver(0, GameElements.Light);
            var result = Battle([root, other, light, dark, bench], [enemy], 103, reverse);
            var frame = result.States.Last(state => state.Tick.Value == 102);
            var hero = reverse ? frame.Opponent : frame.Player;
            var victim = reverse ? frame.Player : frame.Opponent;
            var activations = result.Events.OfType<AbilityActivatedEvent>().Where(e => !e.IsEcho).ToArray();
            if (hero.Mana != 0 || victim.Health != 9980
                || !activations.Where(e => e.SourceCardId == light.EntityId).Select(e => e.Tick.Value).SequenceEqual(new long[] { 100, 102 })
                || activations.Count(e => e.SourceCardId == root.EntityId) != 1
                || activations.Count(e => e.SourceCardId == other.EntityId) != 1
                || activations.Any(e => e.SourceCardId == dark.EntityId || e.SourceCardId == bench.EntityId || e.SourceCardId == enemy.EntityId)
                || frame.Cards.Single(c => c.Id == light.EntityId).CooldownUnits.Single() != 396m
                || frame.Cards.Single(c => c.Id == other.EntityId).CooldownUnits.Single() != 396m) return false;
        }
        return true;
    }

    internal static bool LuksaDestruction()
    {
        foreach (var permanent in new[] { false, true })
        {
            var root = Card(new ArchbishopLuksaCardDefinition(), 5, 0);
            root = root with { Abilities = root.Abilities!.Append(new AbilityDefinition(new StringName("verify.destroy"),
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
                [new DestroyCardEffectDefinition(permanent)])).ToArray() };
            var light = Receiver(2, GameElements.Light) with { Multicast = 1 };
            var result = Battle([root, light], [], 4);
            var frame = result.States.Last(state => state.Tick.Value == 2);
            if (!frame.Cards.Single(c => c.Id == root.EntityId).Destroyed || frame.Opponent.Health != 9980
                || result.Events.OfType<CardDestroyedEvent>().Count() != 1
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == root.EntityId) != 3
                || result.PermanentChanges.Count != (permanent ? 1 : 0)) return false;
        }
        return true;
    }

    internal static bool FallenAngelHealing()
    {
        foreach (var level in new[] { 2, 3, 4 })
        {
            var angel = Card(new FallenAngelCardDefinition(), level, 0);
            var ally = Receiver(2, GameElements.Dark);
            var bench = Receiver(0, GameElements.Dark) with { IsOnBench = true };
            var enemy = Receiver(0, GameElements.Dark) with { Abilities = [new AbilityDefinition(new StringName("verify.injure"),
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(1000)])] };
            var result = Battle([angel, ally, bench], [enemy], 61);
            var frame = result.States.Last(state => state.Tick.Value == 60);
            if (frame.Player.Health != 9000 + 2 * (level == 2 ? 20 : level == 3 ? 40 : 80)
                || frame.Cards.Single(c => c.Id == enemy.EntityId).Immobilize != 10
                || !frame.Cards.Single(c => c.Id == angel.EntityId).IsFlying
                || result.States.First().Cards.Single(c => c.Id == angel.EntityId).IsFlying) return false;
        }
        return true;
    }

    internal static bool FirelockLevelsAndMana()
    {
        foreach (var level in new[] { 1, 2, 3, 4 })
        foreach (var enough in new[] { false, true })
        {
            var card = Card(new ManaSourceFirelockCardDefinition(), level, 0);
            var cost = 60 - level * 10; var damage = 20 << (level - 1); var burn = 6 << (level - 1);
            var result = Battle([card], [], 61, mana: cost - (enough ? 0 : 1));
            var frame = result.States.Last(state => state.Tick.Value == 60);
            if (frame.Opponent.Health != 10000 - (enough ? damage : 0) || frame.Opponent.Burn != (enough ? burn : 0)
                || frame.Player.Mana != (enough ? 0 : cost - 1)
                || result.Events.OfType<AbilityActivatedEvent>().Count() != (enough ? 1 : 0)) return false;
        }
        return true;
    }

    internal static bool AngelFlightTiming()
    {
        CardDefinition[] definitions = [new AngelOfJusticeCardDefinition(), new LittleAngelCardDefinition(), new StarPatternAngelCardDefinition()];
        foreach (var definition in definitions)
        {
            var card = Card(definition, definition.InitialLevel, 0);
            var active = card.Abilities!.Single(a => a.Activation == AbilityActivation.Active);
            var result = Battle([card], [], active.CooldownTicks + 1, mana: 1000);
            var opening = definition is StarPatternAngelCardDefinition;
            if (result.States.Last(s => s.Tick.Value == 0).Cards.Single().IsFlying != opening
                || !result.States.Last(s => s.Tick.Value == active.CooldownTicks).Cards.Single().IsFlying) return false;
        }
        return true;
    }

    private static AbilityDefinition Trigger(int ticks) => new(new StringName("verify.trigger"), AbilityActivation.Active,
        AbilityTarget.SelfCard, 0, ticks, [new TriggerOtherAlliedElementActivesEffectDefinition(GameElements.Light)]);
    private static CardBattleSetup Receiver(int start, StringName element) => new(EntityId.New(), start, 10, 300,
        [new AbilityDefinition(new StringName("verify.receiver"), AbilityActivation.Active, AbilityTarget.EnemyHero, 99, 300,
            [new DamageEffectDefinition(10)])], UseLegacyAttack: false, ElementKeys: [element]);
    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        return new(card.Id, start, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), card.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, ElementKeys: definition.Attributes.Identity.ElementKeys)
            { Level = level, Identity = definition.Attributes.Identity, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }
    private static BattleResult Battle(CardBattleSetup[] own, CardBattleSetup[] enemy, int ticks, bool reverse = false, int mana = 0)
    {
        BattleSideSetup Side(CardBattleSetup[] cards) => new(new HeroBattleSetup(EntityId.New(), 10000, 0,
            MaxMana: 1000, InitialMana: mana, ManaRegen: 0), cards);
        return new CombatSimulator().Simulate(new BattleSetup(Side(reverse ? enemy : own), Side(reverse ? own : enemy),
            42, new BattleTick(ticks), new BattleTick(ticks + 10)));
    }
}
