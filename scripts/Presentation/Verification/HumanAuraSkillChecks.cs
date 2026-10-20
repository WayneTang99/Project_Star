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

// 圣骑士人类属性光环的分级、目标隔离与实际结算验证（表现层）。
internal static class HumanAuraSkillChecks
{
    internal static bool LevelsAndTargets()
    {
        var registry = DefinitionRegistry.Scan(typeof(HumanAuraSkillChecks).Assembly);
        SkillDefinition[] definitions = [new TrainingSkillDefinition(), new RepelEnemiesSkillDefinition()];
        foreach (var definition in definitions)
        for (var level = 1; level <= 4; level++)
        foreach (var opposing in new[] { false, true })
        {
            var texture = GD.Load<Texture2D>(definition.Attributes.Identity.Illustration.ToString());
            if (texture is null || texture.GetWidth() != texture.GetHeight()) return false;
            var key = definition is TrainingSkillDefinition ? GameAttributeKeys.AttackDamage : GameAttributeKeys.Armor;
            var amount = 20 << (level - 1);
            var data = definition.GetLevel(level)!;
            if (!registry.Skills.ContainsKey(definition.Attributes.Identity.Key) || definition.InitialLevel != 1
                || definition.SupportsLevel(0) || definition.SupportsLevel(5)
                || definition.Attributes.Identity.FactionKey != new StringName("paladin")
                || data.Abilities.Single().Activation != AbilityActivation.PassiveAura
                || data.Abilities[0].Effects.Single() is not IncreaseAlliedCardAttributeAuraEffectDefinition aura
                || aura.AttributeKey != key || aura.Amount != amount || aura.RequiredTag != GameTags.Human) return false;
            CardBattleSetup Card(int position, bool human = true) => new(EntityId.New(), position, 5, 11,
                [new AbilityDefinition("ability.verification.human_aura", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 11,
                    [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                        new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)])],
                Tags: new TagSet(human ? [GameTags.Human] : []), UseLegacyAttack: false, ArmorAmount: 5);
            var first = Card(0);
            var second = Card(1);
            var nonhuman = Card(2, false);
            var bench = Card(3) with { IsOnBench = true };
            var enemy = Card(0) with { Abilities = [], CooldownTicks = 100 };
            var skill = new SkillBattleSetup(EntityId.New(), data.Abilities, data.BaseCombatValues);
            var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0),
                [first, second, nonhuman, bench], [skill, skill with { EntityId = EntityId.New() }]);
            var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [enemy]);
            var setup = new BattleSetup(opposing ? other : own, opposing ? own : other, 42, new BattleTick(11), new BattleTick(300));
            var preview = new CombatSimulator().Preview(setup);
            int Value(EntityId id) => preview.Cards.Single(card => card.Id == id).Values[key];
            if (Value(first.EntityId) != 5 + 2 * amount || Value(second.EntityId) != 5 + 2 * amount
                || Value(nonhuman.EntityId) != 5 || Value(bench.EntityId) != 5 || Value(enemy.EntityId) != 5) return false;
            var result = new CombatSimulator().Simulate(setup);
            if (!result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceKind == AbilitySourceKind.Skill)
                || result.PermanentChanges.Count != 0) return false;
            if (key == GameAttributeKeys.AttackDamage
                && result.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == first.EntityId).RawDamage != 5 + 2 * amount) return false;
            if (key == GameAttributeKeys.Armor)
            {
                var frame = result.States.Last(state => state.Tick.Value == 11);
                if ((opposing ? frame.Opponent : frame.Player).Armor != 15 + 4 * amount) return false;
            }
            var text = CardDisplayAdapter.SkillDetails(MatchDisplayQuery.FromSkill(definition, level));
            if (!text.Contains("人类") || !text.Contains("光环") || !text.Contains($"+{amount}")) return false;
        }
        return true;
    }
}
