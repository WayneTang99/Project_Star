using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 神圣感知随机标签、正义天使条件多重与两者联动验证（表现层）。
internal static class DivineSenseAndAngelChecks
{
    internal static bool DivineSense()
    {
        var definition = new DivineSenseSkillDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("skill.divine_sense") || identity.DisplayName != "神圣感知"
            || identity.FactionKey != new StringName("paladin") || definition.InitialLevel != 3
            || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(DivineSenseSkillDefinition).Assembly).Skills.ContainsKey(identity.Key)
            || texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var count in new[] { 0, 1, 4 })
        {
            var enemies = Enumerable.Range(0, count).Select(index => Card(index, GameTags.Human))
                .Append(Card(count, GameTags.Human) with { IsOnBench = true }).ToArray();
            var ally = Card(0, GameTags.Human);
            var skill = Skill(level);
            var setup = Battle([ally], enemies, [skill]);
            var result = new CombatSimulator().Simulate(setup);
            var opening = result.States.Last(frame => frame.Tick.Value == 0);
            var selected = opening.Cards.Where(card => card.QuestTags.Contains(GameTags.Demon)).Select(card => card.Id).ToArray();
            if (selected.Length != System.Math.Min(count, level - 2)
                || opening.Cards.Any(card => !card.QuestTags.Contains(GameTags.Human))
                || selected.Contains(ally.EntityId) || selected.Contains(enemies.Last().EntityId)
                || result.Events.OfType<AbilityActivatedEvent>().Count(entry => entry.SourceCardId == skill.EntityId) != 1
                || enemies.Any(card => card.Tags!.Contains(GameTags.Demon))) return false;
            var repeated = new CombatSimulator().Simulate(setup);
            if (!selected.SequenceEqual(repeated.States.Last(frame => frame.Tick.Value == 0).Cards
                    .Where(card => card.QuestTags.Contains(GameTags.Demon)).Select(card => card.Id))) return false;
            var fresh = new CombatSimulator().Simulate(Battle([ally], enemies, []));
            if (fresh.States.Any(frame => frame.Cards.Any(card => card.QuestTags.Contains(GameTags.Demon)))) return false;
        }
        return true;
    }

    internal static bool Angel()
    {
        var definition = new AngelOfJusticeCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.angel_of_justice") || identity.DisplayName != "正义天使"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light }) || definition.Tags.Count != 1
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(AngelOfJusticeCardDefinition).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        foreach (var level in new[] { 2, 3, 4 })
        {
            var entity = new EntityFactory().CreateCard(definition, level);
            var damage = 60 << (level - 2);
            var angel = new CardBattleSetup(entity.Id, 0, entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
                entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), entity.Abilities,
                UseLegacyAttack: false, Tags: entity.Tags, OccupiedSlots: 2, ElementKeys: identity.ElementKeys);
            if (angel.AttackDamage != damage || angel.CooldownTicks != 40) return false;
            foreach (var tag in new[] { GameTags.Human, GameTags.Demon, GameTags.Undead })
            {
                var enemy = Card(0, tag);
                var result = new CombatSimulator().Simulate(Battle([angel], [enemy], []));
                var expected = tag == GameTags.Human ? new long[] { 40 } : new long[] { 40, 42 };
                if (!Hits(result, angel).Select(hit => hit.Tick.Value).SequenceEqual(expected)
                    || Hits(result, angel).Any(hit => hit.RawDamage != damage)) return false;
            }
            var human = Card(0, GameTags.Human);
            var linked = new CombatSimulator().Simulate(Battle([angel], [human], [Skill(3)]));
            if (!Hits(linked, angel).Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 40, 42 })) return false;
            var demon = Card(0, GameTags.Demon) with { Tags = new TagSet([GameTags.Demon, GameTags.Undead]) };
            var many = new CombatSimulator().Simulate(Battle([angel], [demon, Card(1, GameTags.Undead)], []));
            if (Hits(many, angel).Length != 2) return false;
            var bench = new CombatSimulator().Simulate(Battle([angel], [demon with { IsOnBench = true }], []));
            if (Hits(bench, angel).Length != 1) return false;
            var destroy = new AbilityDefinition("verification.angel.destroy", AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 20, [new DestroyCardEffectDefinition(false)]);
            var removed = new CombatSimulator().Simulate(Battle([angel], [demon with { Abilities = [destroy] }], []));
            if (Hits(removed, angel).Length != 1
                || removed.States.Last(frame => frame.Tick.Value == 20).Cards.Single(card => card.Id == angel.EntityId)
                    .Values[GameAttributeKeys.Multicast] != 0) return false;
        }
        return true;
    }

    private static DamageDealtEvent[] Hits(BattleResult result, CardBattleSetup card) => result.Events
        .OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();

    private static CardBattleSetup Card(int position, StringName tag) => new(EntityId.New(), position, 0, 0, [],
        UseLegacyAttack: false, Tags: new TagSet([tag]));

    private static SkillBattleSetup Skill(int level)
    {
        var skill = new EntityFactory().CreateSkill(new DivineSenseSkillDefinition(), level);
        return new SkillBattleSetup(skill.Id, skill.Abilities, skill.Attributes.BaseCombat.SnapshotFinalValues());
    }

    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, SkillBattleSetup[] skills) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies, skills),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies),
        42, new BattleTick(43), new BattleTick(44));
}
