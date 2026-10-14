using System;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 无阵营属性光环技能的等级、筛选、动态位置及实际结算验证（表现层）。
internal static class NeutralAuraSkillChecks
{
    private static SkillDefinition[] Definitions() =>
    [new SharpnessSkillDefinition(), new LeftHandBladeSkillDefinition(), new RightHandBladeSkillDefinition(),
        new GuardSkillDefinition(), new LeftHandShieldSkillDefinition(), new RightHandShieldSkillDefinition(),
        new HealingSkillDefinition(), new LeftHandStaffSkillDefinition(), new RightHandStaffSkillDefinition(),
        new PoisonMasterySkillDefinition(), new BurnMasterySkillDefinition()];

    internal static bool LevelsAndTargets()
    {
        var registry = DefinitionRegistry.Scan(typeof(NeutralAuraSkillChecks).Assembly);
        var definitions = Definitions();
        StringName[] attributes = [GameAttributeKeys.AttackDamage, GameAttributeKeys.Armor, GameAttributeKeys.HealingBonus,
            GameAttributeKeys.PoisonBonus, GameAttributeKeys.BurnBonus];
        for (var index = 0; index < definitions.Length; index++)
        for (var level = 1; level <= 4; level++)
        {
            var skill = definitions[index];
            var ability = skill.GetLevel(level)!.Abilities.Single();
            var aura = (IncreaseAlliedCardAttributeAuraEffectDefinition)ability.Effects.Single();
            var key = attributes[index < 9 ? index / 3 : index - 6];
            var side = index < 9 && index % 3 != 0 ? (AdjacentCardSide?)(index % 3 == 1 ? AdjacentCardSide.Left : AdjacentCardSide.Right) : null;
            var amount = (index >= 9 ? 2 : side is null ? 10 : 20) << (level - 1);
            if (!registry.Skills.ContainsKey(skill.Attributes.Identity.Key) || skill.InitialLevel != 1 || skill.SupportsLevel(5)
                || skill.Attributes.Identity.FactionKey != GameFactions.Neutral || ability.Activation != AbilityActivation.PassiveAura
                || ability.ManaCost != 0 || ability.CooldownTicks != 0 || aura.AttributeKey != key
                || aura.ExtremeSide != side || aura.Amount != amount) return false;
            var left = Receiver(key, 2) with { OccupiedSlots = 2 };
            var right = Receiver(key, 7);
            var unsupported = new CardBattleSetup(EntityId.New(), 0, 0, 1, UseLegacyAttack: false);
            var bench = Receiver(key, 0) with { IsOnBench = true };
            var enemy = Receiver(key, 1);
            foreach (var opposing in new[] { false, true })
            {
                var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0),
                    [right, unsupported, bench, left], [Skill(skill, level), Skill(skill, level)]);
                var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [enemy]);
                var setup = new BattleSetup(opposing ? other : own, opposing ? own : other, 19, new BattleTick(11), new BattleTick(300));
                var frame = new CombatSimulator().Preview(setup);
                int Value(EntityId id) => frame.Cards.Single(card => card.Id == id).Values.TryGetValue(key, out var value) ? value : 0;
                if (Value(left.EntityId) != Base(key) + (side == AdjacentCardSide.Right ? 0 : 2 * amount)
                    || Value(right.EntityId) != Base(key) + (side == AdjacentCardSide.Left ? 0 : 2 * amount)
                    || Value(bench.EntityId) != Base(key) || Value(enemy.EntityId) != Base(key)) return false;
                var result = new CombatSimulator().Simulate(setup);
                if (!result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                    || result.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceKind == AbilitySourceKind.Skill)
                    || result.PermanentChanges.Count != 0) return false;
                if (key == GameAttributeKeys.AttackDamage
                    && result.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == left.EntityId).RawDamage != Value(left.EntityId)) return false;
                if (key == GameAttributeKeys.Armor)
                {
                    var hero = result.States.Last(frame => frame.Tick.Value == 11).Player;
                    if (opposing) hero = result.States.Last(frame => frame.Tick.Value == 11).Opponent;
                    if (hero.Armor != 10 + (side is null ? 4 : 2) * amount) return false;
                }
                if (key == GameAttributeKeys.PoisonBonus || key == GameAttributeKeys.BurnBonus)
                {
                    var status = key == GameAttributeKeys.PoisonBonus ? BattleStatus.Poison : BattleStatus.Burn;
                    var applied = result.Events.OfType<StatusChangedEvent>().Where(change => change.Tick.Value == 11 && change.Status == status).Sum(change => change.Amount);
                    if (applied != 15 + (side is null ? 4 : 2) * amount) return false;
                }
            }
            var text = CardDisplayAdapter.SkillDetails(MatchDisplayQuery.FromSkill(skill, level));
            if (!text.Contains("光环") || !text.Contains($"+{amount}")
                || side is { } extreme && !text.Contains(extreme == AdjacentCardSide.Left ? "最左侧" : "最右侧")) return false;
            var empty = new BattleSetup(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [], [Skill(skill, level)]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), []), 1, new BattleTick(1));
            if (new CombatSimulator().Preview(empty).Cards.Count != 0) return false;
        }
        return true;
    }

    internal static bool DynamicPositions()
    {
        foreach (var skill in Definitions().Where(skill => ((IncreaseAlliedCardAttributeAuraEffectDefinition)skill.GetLevel(1)!.Abilities[0].Effects[0]).ExtremeSide is not null))
        {
            var aura = (IncreaseAlliedCardAttributeAuraEffectDefinition)skill.GetLevel(1)!.Abilities[0].Effects[0];
            var edge = Receiver(aura.AttributeKey, aura.ExtremeSide == AdjacentCardSide.Left ? 0 : 7);
            edge = edge with { Abilities = edge.Abilities!.Append(new AbilityDefinition("ability.verification.edge_destroy",
                AbilityActivation.Active, AbilityTarget.SelfCard, 0, 1, [new DestroyCardEffectDefinition(false)])).ToArray() };
            var middle = Receiver(aura.AttributeKey, 3);
            var setup = new BattleSetup(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [middle, edge], [Skill(skill, 4)]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), []), 1, new BattleTick(10), new BattleTick(300));
            var result = new CombatSimulator().Simulate(setup);
            var before = result.States.First().Cards.Single(card => card.Id == middle.EntityId);
            var after = result.States.Last(frame => frame.Tick.Value == 10).Cards.Single(card => card.Id == middle.EntityId);
            if ((before.Values.TryGetValue(aura.AttributeKey, out var original) ? original : 0) != Base(aura.AttributeKey)
                || after.Values[aura.AttributeKey] != Base(aura.AttributeKey) + 160
                || result.Events.OfType<CardDestroyedEvent>().Count() != 1) return false;
        }
        return true;
    }

    internal static bool PoisonFormulasAndHealing()
    {
        EffectDefinition[] effects = [new ApplyStatusEffectDefinition(BattleStatus.Poison, 5),
            new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Value),
            new ApplySourceAttributePercentStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.AttackDamage, 50),
            new ApplyPrimaryEffectPercentStatusEffectDefinition(BattleStatus.Poison, 50)];
        int[] expected = [21, 27, 21, 21];
        for (var index = 0; index < effects.Length; index++)
        {
            var card = new CardBattleSetup(EntityId.New(), 0, 10, 2,
                [new AbilityDefinition("ability.verification.poison_formula", AbilityActivation.Active, AbilityTarget.EnemyHero,
                    0, 2, index == 3 ? [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage), effects[index]] : [effects[index]])],
                UseLegacyAttack: false) { CombatValues = new System.Collections.Generic.Dictionary<StringName, int> { [GameAttributeKeys.Value] = 11 } };
            var setup = new BattleSetup(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [card], [Skill(new PoisonMasterySkillDefinition(), 4)]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), []), 7, new BattleTick(10), new BattleTick(300));
            var result = new CombatSimulator().Simulate(setup);
            if (result.Events.OfType<StatusChangedEvent>().Single(change => change.Status == BattleStatus.Poison).Amount != expected[index]) { GD.Print($"中毒公式{index}：实际{result.Events.OfType<StatusChangedEvent>().Single(change => change.Status == BattleStatus.Poison).Amount}，预期{expected[index]}"); return false; }
        }
        // 先受伤再治疗，检查每次治疗贡献及最大生命截断。
        foreach (var skill in Definitions().Skip(6).Take(3))
        for (var level = 1; level <= 4; level++)
        {
            var healer = Receiver(GameAttributeKeys.HealingBonus, 3);
            var attacker = Receiver(GameAttributeKeys.AttackDamage, 0) with { AttackDamage = 500,
                Abilities = [new AbilityDefinition("ability.verification.hurt", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 10,
                    [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])] };
            var setup = new BattleSetup(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [healer], [Skill(skill, level)]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [attacker]), 1, new BattleTick(11), new BattleTick(300));
            var frame = new CombatSimulator().Simulate(setup).States.Last(state => state.Tick.Value == 11 && state.Player.Health > 0);
            var amount = ((IncreaseAlliedCardAttributeAuraEffectDefinition)skill.GetLevel(level)!.Abilities[0].Effects[0]).Amount;
            if (frame.Player.Health != 9500 + 5 + amount) { GD.Print($"治疗{skill.Attributes.Identity.DisplayName} {level}：实际{frame.Player.Health}，预期{9500 + 5 + amount}"); return false; }
        }
        return true;
    }

    private static int Base(StringName key) => key == GameAttributeKeys.AttackDamage || key == GameAttributeKeys.Armor ? 5 : 0;
    private static SkillBattleSetup Skill(SkillDefinition definition, int level) => new(EntityId.New(), definition.GetLevel(level)!.Abilities, definition.GetLevel(level)!.BaseCombatValues);
    private static CardBattleSetup Receiver(StringName key, int position)
    {
        EffectDefinition effect = key == GameAttributeKeys.AttackDamage ? new AttributeDamageEffectDefinition(key)
            : key == GameAttributeKeys.Armor ? new GainSourceHeroArmorFromAttributeEffectDefinition(key)
            : key == GameAttributeKeys.HealingBonus ? new HealEffectDefinition(5, TargetsSourceHero: true)
            : new ApplyStatusEffectDefinition(key == GameAttributeKeys.PoisonBonus ? BattleStatus.Poison : BattleStatus.Burn, 5);
        return new CardBattleSetup(EntityId.New(), position, 5, 2,
            [new AbilityDefinition("ability.verification.receiver", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 11, [effect])],
            UseLegacyAttack: false, ArmorAmount: 5);
    }
}
