using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 秩序板甲分级护甲、单目标迟缓与无敌方目标验证（表现层）。
internal static class OrderPlateArmorChecks
{
    internal static bool ArmorAndSlow()
    {
        var definition = new OrderPlateArmorCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "秩序板甲" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Medium || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.ToArray().OrderBy(tag => tag.ToString()).SequenceEqual(
                new[] { GameTags.Equipment, GameTags.Medium }.OrderBy(tag => tag.ToString()))) return false;
        for (var level = 1; level <= 4; level++)
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var abilities = definition.GetLevel(level)!.Abilities;
            var armor = 20 + level * 20;
            if (instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) != armor
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 50
                || abilities.Single().CooldownTicks != 50 || abilities.Single().ManaCost != 0) return false;
            var source = new CardBattleSetup(instance.Id, 0, 0, 50, abilities, UseLegacyAttack: false,
                Tags: definition.Tags, OccupiedSlots: 2, ArmorAmount: armor) { Level = level };
            var first = Target(0);
            var second = Target(1);
            var bench = Target(0) with { IsOnBench = true };
            var setup = Battle([source], [first, second, bench], 100);
            var simulator = new CombatSimulator();
            var result = simulator.Simulate(setup);
            var slows = result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.SlowDuration).ToArray();
            if (slows.Length != 2 || slows.Any(item => item.Amount != level * 10)
                || !slows.Select(item => item.Tick.Value).SequenceEqual(new long[] { 50, 100 })
                || !result.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceCardId == source.EntityId)
                    .Select(item => item.Tick.Value).SequenceEqual(new long[] { 50, 100 })
                || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)
                || result.PermanentChanges.Count != 0) return false;
            var frame = result.States.Last(state => state.Tick.Value == 50);
            if (frame.Player.Armor != armor
                || frame.Cards.Count(card => (card.Id == first.EntityId || card.Id == second.EntityId) && card.Slow == level * 10) != 1
                || frame.Cards.Single(card => card.Id == bench.EntityId).Slow != 0
                || frame.Cards.Single(card => card.Id == source.EntityId).Slow != 0
                || result.States.Last(state => state.Player.Health > 0).Player.Armor != 2 * armor) return false;
            // 无可选目标仍获得护甲；守护旗帜增强已有护甲能力。
            var bannerDefinition = new GuardianStandardCardDefinition();
            var banner = new CardBattleSetup(EntityId.New(), 2, 0, 0, bannerDefinition.GetLevel(2)!.Abilities,
                UseLegacyAttack: false, Tags: bannerDefinition.Tags) { Level = 2 };
            result = simulator.Simulate(Battle([source, banner], [], 50));
            if (result.States.Last(state => state.Player.Health > 0).Player.Armor != armor + 20
                || result.Events.OfType<StatusChangedEvent>().Any()
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) != armor) return false;
        }
        return true;
    }

    private static CardBattleSetup Target(int start) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false);

    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), enemies), 42,
        new BattleTick(ticks), new BattleTick(ticks + 1));
}
