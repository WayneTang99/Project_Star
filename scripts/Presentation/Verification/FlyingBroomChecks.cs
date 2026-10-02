using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 飞天扫帚等级数据及进入飞行回响验证（表现层验证模块）。
internal static class FlyingBroomChecks
{
    internal static bool LevelsAndActivation()
    {
        var definition = new FlyingBroomCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.flying_broom") || identity.DisplayName != "飞天扫帚"
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Equipment) || !definition.Tags.Contains(GameTags.Mount)
            || !definition.Tags.Contains(GameTags.Medium) || definition.InitialLevel != 1
            || definition.SupportsLevel(5)) return false;
        for (var level = 1; level <= 4; level++)
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var cooldown = (6 - level) * 10;
            var abilities = definition.GetLevel(level)!.Abilities;
            if (instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != cooldown
                || abilities[0].CooldownTicks != cooldown || abilities.Any(ability => ability.ManaCost != 0)) return false;
            var card = new CardBattleSetup(instance.Id, 0, 0, cooldown, abilities, UseLegacyAttack: false);
            var result = Simulate([card], [], cooldown + 50);
            var entries = result.Events.OfType<CardStateChangedEvent>().ToArray();
            if (entries.Length != 1 || entries[0].Tick.Value != cooldown || !entries[0].Enabled
                || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.IsEcho) != 1
                || result.Events.OfType<StatusChangedEvent>().Single().Amount != 10
                || result.States.Last(frame => frame.Tick.Value == cooldown).Cards.Single().Haste != 10
                || result.States[^1].Cards.Single().Haste != 0) return false;
        }
        return true;
    }

    internal static bool EventTargetsAndStacking()
    {
        var abilities = new FlyingBroomCardDefinition().GetLevel(1)!.Abilities;
        var first = EntityId.New(); var second = EntityId.New();
        var ally = EntityId.New(); var enemy = EntityId.New(); var bench = EntityId.New();
        AbilityDefinition Enter(string key, AbilityActivation activation, int cooldown, bool flying = true) =>
            new(new StringName("verification.broom." + key), activation, AbilityTarget.SelfCard, 0, cooldown,
                [new SetSourceCardStateEffectDefinition(flying ? GameAttributeKeys.Flying : GameAttributeKeys.Berserk, true)]);
        CardBattleSetup Broom(EntityId id, int start, bool onBench = false) =>
            new(id, start, 0, 50, abilities, IsOnBench: onBench, UseLegacyAttack: false);
        var setup = new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0),
                [Broom(first, 0), Broom(second, 2), Broom(bench, 4, true),
                 new CardBattleSetup(ally, 6, 0, 1,
                    [Enter("ally", AbilityActivation.Active, 1), Enter("berserk", AbilityActivation.PassiveOnBattleStart, 0, false)],
                    UseLegacyAttack: false)]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0),
                [new CardBattleSetup(enemy, 0, 0, 1, [Enter("enemy", AbilityActivation.Active, 1)], UseLegacyAttack: false)]),
            42, new BattleTick(2));
        var result = new CombatSimulator().Simulate(setup);
        var cards = result.States.Last(frame => frame.Tick.Value == 1).Cards;
        if (cards.Single(card => card.Id == ally).Haste != 20
            || cards.Single(card => card.Id == enemy).Haste != 0
            || cards.Single(card => card.Id == first).Haste != 0
            || cards.Single(card => card.Id == second).Haste != 0
            || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.IsEcho) != 2
            || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        var echoEntry = Enter("echo_entry", AbilityActivation.EchoOnAbilityActivated, 0);
        var chain = Simulate([new CardBattleSetup(ally, 0, 0, 1,
            [Enter("berserk_active", AbilityActivation.Active, 1, false), echoEntry], UseLegacyAttack: false),
            Broom(first, 1)], [], 2);
        return chain.Events.OfType<CardStateChangedEvent>().Any(item => item.StateKey == GameAttributeKeys.Flying)
            && !chain.Events.OfType<StatusChangedEvent>().Any();
    }

    private static BattleResult Simulate(CardBattleSetup[] allies, CardBattleSetup[] enemies, int timeout) =>
        new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0), allies),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0), enemies),
            42, new BattleTick(timeout)));
}
