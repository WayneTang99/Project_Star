using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 小天使治疗成长、双标签去重、多重及神圣感知联动验证（表现层）。
internal static class LittleAngelChecks
{
    internal static bool LevelsAndHealing()
    {
        var definition = new LittleAngelCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.DisplayName != "小天使" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || definition.InitialLevel != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
        for (var level = 1; level <= 4; level++)
        {
            var entity = new EntityFactory().CreateCard(definition, level);
            var angel = new CardBattleSetup(entity.Id, 0, 0, 50, entity.Abilities, UseLegacyAttack: false);
            var healing = 10 << (level - 1);
            var bonus = level * 5;
            foreach (var tags in new[] { new TagSet([GameTags.Human]), new TagSet([GameTags.Demon]),
                new TagSet([GameTags.Undead]), new TagSet([GameTags.Demon, GameTags.Undead]) })
            foreach (var multicast in new[] { 0, 1 })
            {
                var enemy = new CardBattleSetup(EntityId.New(), 0, 100, 20, Multicast: multicast, Tags: tags);
                var setup = Battle(angel, enemy);
                var result = new CombatSimulator().Simulate(setup);
                var qualifies = tags.Contains(GameTags.Demon) || tags.Contains(GameTags.Undead);
                var count = qualifies ? 2 * (multicast + 1) : 0;
                var frame = result.States.Last(state => state.Tick.Value == 50);
                var growth = result.Events.OfType<CardAttributeChangedEvent>().Where(change => change.CardId == angel.EntityId).ToArray();
                if (growth.Length != count || growth.Any(change => change.AttributeKey != GameAttributeKeys.HealingBonus || change.Amount != bonus)
                    || frame.Player.Health != 10000 - 200 * (multicast + 1) + healing + count * bonus
                    || result.PermanentAttributeBonuses.Any()
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            var human = new CardBattleSetup(EntityId.New(), 0, 100, 20, Tags: new TagSet([GameTags.Human]));
            var skill = new EntityFactory().CreateSkill(new DivineSenseSkillDefinition(), 3);
            var linked = new CombatSimulator().Simulate(Battle(angel, human,
                [new SkillBattleSetup(skill.Id, skill.Abilities, skill.Attributes.BaseCombat.SnapshotFinalValues())]));
            if (linked.Events.OfType<CardAttributeChangedEvent>().Count(change => change.CardId == angel.EntityId) != 2
                || entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HealingBonus) != 0) return false;
            var bench = new CombatSimulator().Simulate(Battle(angel,
                human with { IsOnBench = true, Tags = new TagSet([GameTags.Demon]) }));
            if (bench.Events.OfType<CardAttributeChangedEvent>().Any()) return false;
        }
        return true;
    }

    private static BattleSetup Battle(CardBattleSetup angel, CardBattleSetup enemy, SkillBattleSetup[]? skills = null) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [angel], skills),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [enemy]),
        42, new BattleTick(51), new BattleTick(52));
}
