using System;
using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 魔法药水恢复量、上限、零消耗与自毁顺序验证（表现层验证模块）。
internal static class SmallManaPotionChecks
{
    internal static bool RestoreAndConsume()
    {
        var definition = new SmallManaPotionCardDefinition();
        if (definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || definition.Attributes.Identity.DisplayName != "小型魔法药水"
            || definition.Attributes.Identity.FactionKey != new StringName("mona")
            || definition.Attributes.Identity.Size != CardSize.Small
            || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Consumable)) return false;
        int[] values = [40, 80, 160, 240];
        for (var level = 1; level <= 4; level++)
        foreach (var (maximum, initial) in new[] { (1000, 0), (100, 90), (100, 100) })
        {
            var id = EntityId.New();
            var ability = definition.GetLevel(level)!.Abilities.Single();
            if (ability.ManaCost != 0 || ability.CooldownTicks != 30) return false;
            var setup = new BattleSetup(
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0, maximum, initial, ManaRegen: 0),
                    [new CardBattleSetup(id, 0, 0, 30, [ability], UseLegacyAttack: false, Multicast: 2)]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0), []), 42, new BattleTick(65));
            var simulator = new CombatSimulator(); var result = simulator.Simulate(setup);
            var change = result.Events.OfType<ManaChangedEvent>().Single();
            var amount = Math.Min(values[level - 1], maximum - initial);
            var destroyed = result.Events.OfType<CardDestroyedEvent>().Single();
            if (change.Amount != amount || change.CurrentMana != initial + amount || change.Tick.Value != 30
                || result.Events.ToList().IndexOf(change) >= result.Events.ToList().IndexOf(destroyed)
                || result.Events.OfType<AbilityActivatedEvent>().Count() != 1
                || result.States[^1].Player.Mana != initial + amount || result.PermanentChanges.Count != 0
                || !result.States[^1].Cards.Single().Destroyed
                || simulator.Simulate(setup).States[0].Cards.Single().Destroyed) return false;
        }
        return true;
    }
}
