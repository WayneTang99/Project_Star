using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 投石车分级、动态人类计数与有效完整冷却攻击光环验证（表现层）。
internal static class CatapultChecks
{
    internal static bool LevelsAndAuras()
    {
        var definition = new CatapultCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.catapult") || identity.DisplayName != "投石车"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Large
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Mechanical)
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(CatapultCardDefinition).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        for (var level = 2; level <= 4; level++)
        {
            var entity = new EntityFactory().CreateCard(definition, level);
            var damage = 80 << (level - 2);
            var factor = level * 10;
            var card = new CardBattleSetup(entity.Id, 0,
                entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
                entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), entity.Abilities,
                UseLegacyAttack: false, Tags: entity.Tags, OccupiedSlots: 3);
            if (card.AttackDamage != damage || card.CooldownTicks != 120) return false;
            foreach (var count in new[] { 0, 1, 3 })
            foreach (var multiplier in new[] { 1m, .5m, 0m })
            foreach (var side in new[] { SideId.Player, SideId.Opponent })
            {
                var allies = new[] { card with { CooldownMultiplier = multiplier } }
                    .Concat(Enumerable.Range(0, count).Select(index => Human(index + 3))).ToArray();
                var setup = Battle(allies, [], side);
                var duration = System.Math.Max(1m, (12 - count) * multiplier);
                var expected = damage + (int)decimal.Floor(duration * factor);
                var preview = new CombatSimulator().Preview(setup).Cards.Single(item => item.Id == card.EntityId);
                if (preview.Values[GameAttributeKeys.AttackDamage] != expected
                    || preview.CooldownDurationUnits.Single() != duration * 20) return false;
                var result = new CombatSimulator().Simulate(setup);
                var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();
                if (hits.Length == 0 || hits[0].Tick.Value != duration * 10
                    || hits.Any(hit => hit.RawDamage != expected || hit.TargetSide == side)
                    || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                    || result.Events.OfType<ManaChangedEvent>().Any()
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            var human = Human(3);
            var destroy = new AbilityDefinition("verification.catapult.destroy", AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 20, [new DestroyCardEffectDefinition(false)]);
            var dynamicSetup = Battle([card, human with { Abilities = [destroy] }], []);
            var dynamicResult = new CombatSimulator().Simulate(dynamicSetup);
            var before = dynamicResult.States.Last(frame => frame.Tick.Value == 19).Cards.Single(item => item.Id == card.EntityId);
            var after = dynamicResult.States.Last(frame => frame.Tick.Value == 20).Cards.Single(item => item.Id == card.EntityId);
            if (before.Values[GameAttributeKeys.AttackDamage] != damage + 11 * factor
                || after.Values[GameAttributeKeys.AttackDamage] != damage + 12 * factor
                || after.CooldownDurationUnits.Single() != 240 || after.CooldownUnits.Single() != 200
                || dynamicResult.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == card.EntityId).Tick.Value != 120) return false;
            var bench = human with { IsOnBench = true };
            var beast = human with { EntityId = EntityId.New(), BoardStart = 4, Tags = new TagSet([GameTags.Beast]) };
            var excluded = new CombatSimulator().Preview(Battle([card, bench, beast], [Human(0)]));
            if (excluded.Cards.Single(item => item.Id == card.EntityId).Values[GameAttributeKeys.AttackDamage] != damage + 12 * factor) return false;
            var inactive = new CombatSimulator().Preview(Battle([card with { IsOnBench = true }, human], []));
            if (inactive.Cards.Single(item => item.Id == card.EntityId).Values[GameAttributeKeys.AttackDamage] != damage
                || entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != damage) return false;
        }
        return true;
    }

    private static CardBattleSetup Human(int position) => new(EntityId.New(), position, 0, 0, [],
        UseLegacyAttack: false, Tags: new TagSet([GameTags.Human]));

    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, SideId side = SideId.Player)
    {
        var friendly = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var hostile = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(side == SideId.Player ? friendly : hostile, side == SideId.Player ? hostile : friendly,
            42, new BattleTick(121), new BattleTick(122));
    }
}
