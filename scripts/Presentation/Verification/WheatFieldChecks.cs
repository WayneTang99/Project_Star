using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 麦田分级、全体标签充能、即时发动与人类疾速回响验证（表现层）。
internal static class WheatFieldChecks
{
    internal static bool LevelsAndCharge()
    {
        var definition = new WheatFieldCardDefinition();
        if (definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || definition.Attributes.Identity.Size != CardSize.Large
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.Wood })
            || definition.Tags.Count != 3 || !definition.Tags.Contains(GameTags.Plant)
            || !definition.Tags.Contains(GameTags.Location) || TagDisplayNames.Get(GameTags.Plant) != "植物") return false;
        for (var level = 2; level <= 4; level++)
        {
            var cooldown = 140 - level * 10;
            var instance = new EntityFactory().CreateCard(definition, level);
            if (instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != cooldown) return false;
            var field = Field(level);
            var first = Attacker(3, cooldown + 5, true) with { Multicast = 1 };
            var second = Attacker(4, cooldown + 5, true);
            var other = Attacker(5, cooldown + 5, false);
            var bench = Attacker(0, cooldown + 5, true) with { IsOnBench = true };
            var enemy = Attacker(0, cooldown + 5, true);
            var dead = Attacker(6, cooldown + 5, true) with
            {
                Abilities = [new AbilityDefinition(new StringName("verification.wheat.destroy"),
                    AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])]
            };
            var setup = Battle([field, first, second, other, bench, dead], [enemy], cooldown);
            var simulator = new CombatSimulator();
            var result = simulator.Simulate(setup);
            var charges = result.Events.OfType<CardChargedEvent>().ToArray();
            if (charges.Length != 2 || charges.Any(e => e.SourceCardId != field.EntityId || e.AmountTicks != 10
                || e.Tick.Value != cooldown || e.TargetCardId != first.EntityId && e.TargetCardId != second.EntityId)
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => !e.IsEcho && e.SourceCardId == first.EntityId) != 2
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => !e.IsEcho && e.SourceCardId == second.EntityId) != 1
                || result.Events.OfType<StatusChangedEvent>().Count(e => e.Status == BattleStatus.HasteDuration && e.Amount == 10) != 3
                || result.States.Last().Cards.Single(card => card.Id == field.EntityId).Haste != 30
                || !result.Events.SequenceEqual(simulator.Simulate(setup).Events) || result.PermanentChanges.Count != 0) return false;
        }
        return true;
    }

    internal static bool EchoAcceleratesCooldown()
    {
        var field = Field(2);
        var human = Attacker(3, 20, true) with { Multicast = 1 };
        var result = new CombatSimulator().Simulate(Battle([field, human], [], 70));
        var at20 = result.States.Last(frame => frame.Tick.Value == 20);
        if (at20.Cards.Single(card => card.Id == field.EntityId).Haste != 20
            || result.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == field.EntityId && !e.IsEcho).Tick.Value != 70)
            return false;
        // 敌方人类、己方非人类和备战人类不会触发麦田回响；无目标时发动仍结束。
        result = new CombatSimulator().Simulate(Battle([field, Attacker(3, 20, false),
            Attacker(0, 20, true) with { IsOnBench = true }], [Attacker(0, 20, true)], 120));
        return !result.Events.OfType<CardChargedEvent>().Any()
            && !result.Events.OfType<AbilityActivatedEvent>().Any(e => e.SourceCardId == field.EntityId && e.IsEcho)
            && result.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == field.EntityId).Tick.Value == 120;
    }

    private static CardBattleSetup Field(int level) => new(EntityId.New(), 0, 0, 140 - level * 10,
        new WheatFieldCardDefinition().GetLevel(level)!.Abilities, UseLegacyAttack: false,
        Tags: new TagSet([GameTags.Plant, GameTags.Location]), OccupiedSlots: 3, ElementKeys: [GameElements.Wood]) { Level = level };

    private static CardBattleSetup Attacker(int start, int ticks, bool human) => new(EntityId.New(), start, 1, ticks,
        [new AbilityDefinition(new StringName("verification.wheat.attack"), AbilityActivation.Active, AbilityTarget.EnemyHero,
            0, ticks, [new DamageEffectDefinition(1)])], UseLegacyAttack: false, Tags: new TagSet(human ? [GameTags.Human] : []));

    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), enemies), 42,
        new BattleTick(ticks), new BattleTick(ticks + 1));
}
