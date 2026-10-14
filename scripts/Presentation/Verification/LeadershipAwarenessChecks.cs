using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 领袖意识首次人类发动、充能目标与确定性验证（表现层验证模块）。
internal static class LeadershipAwarenessChecks
{
    internal static bool LevelsAndFirstActivation()
    {
        var definition = new LeadershipAwarenessSkillDefinition();
        if (definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var opposing in new[] { false, true })
        {
            var skill = new EntityFactory().CreateSkill(definition, level);
            var amount = (level - 2) * 10;
            if (skill.Abilities[0].Effects[0] is not ChargeTaggedAlliedCardsEffectDefinition
                { ExcludeEventCard: true } effect || effect.AmountTicks != amount) return false;
            CardBattleSetup Card(int position, int cooldown, bool human = true) => new(
                EntityId.New(), position, 0, cooldown, Tags: new TagSet(human ? [GameTags.Human] : []));
            var nonhuman = Card(0, 1, false);
            var first = Card(1, 10) with { Multicast = 2 };
            var second = Card(2, 40);
            var third = Card(3, 50);
            var bench = Card(4, 1) with { IsOnBench = true };
            var enemy = Card(0, 1);
            var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0),
                [nonhuman, first, second, third, bench],
                [new SkillBattleSetup(skill.Id, skill.Abilities, skill.Attributes.BaseCombat.SnapshotFinalValues())]);
            var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [enemy]);
            var setup = new BattleSetup(opposing ? other : own, opposing ? own : other,
                42, new BattleTick(100), new BattleTick(300));
            var result = new CombatSimulator().Simulate(setup);
            var charges = result.Events.OfType<CardChargedEvent>().Where(item => item.SourceCardId == skill.Id).ToArray();
            var echoes = result.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceCardId == skill.Id).ToArray();
            if (echoes.Length != 1 || !echoes[0].IsEcho || echoes[0].Tick != new BattleTick(10)
                || charges.Length != 2 || charges.Any(item => item.Tick != new BattleTick(10) || item.AmountTicks != amount)
                || !charges.Select(item => item.TargetCardId).ToHashSet().SetEquals([second.EntityId, third.EntityId])
                || result.Events.OfType<AbilityActivatedEvent>().First(item => item.SourceCardId == second.EntityId).Tick != new BattleTick(40 - amount)
                || result.Events.OfType<AbilityActivatedEvent>().First(item => item.SourceCardId == third.EntityId).Tick != new BattleTick(50 - amount)
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;

            // 没有人类发动时不触发；没有其他人类时仍消耗本场的一次回响。
            foreach (var cards in new[] { new[] { nonhuman, bench }, new[] { first, nonhuman, bench } })
            {
                var empty = new BattleSideSetup(own.Hero, cards, own.Skills);
                var events = new CombatSimulator().Simulate(new BattleSetup(empty, other, 42,
                    new BattleTick(30), new BattleTick(300))).Events;
                if (events.OfType<CardChargedEvent>().Any(item => item.SourceCardId == skill.Id)
                    || events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == skill.Id)
                        != (cards.Contains(first) ? 1 : 0)) return false;
            }
        }
        return true;
    }
}
