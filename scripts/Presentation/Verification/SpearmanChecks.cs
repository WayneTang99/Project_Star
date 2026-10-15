using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 枪矛手分级、相邻人类成长及战斗隔离验证（表现层）。
internal static class SpearmanChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new SpearmanCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.spearman") || identity.DisplayName != "枪矛手"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(SpearmanCardDefinition).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
        for (var level = 1; level <= 4; level++)
        {
            var entity = new EntityFactory().CreateCard(definition, level);
            var damage = 10 << (level - 1);
            var bonus = level * 5;
            var card = new CardBattleSetup(entity.Id, 3,
                entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
                entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), entity.Abilities,
                UseLegacyAttack: false, Tags: entity.Tags, OccupiedSlots: 1);
            var solo = Run([card], []);
            var hits = solo.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();
            if (card.AttackDamage != damage || card.CooldownTicks != 60 || hits.Length != 1
                || hits[0].Tick.Value != 60 || hits[0].RawDamage != damage
                || solo.Events.OfType<ManaChangedEvent>().Any()) return false;
            var left = Neighbor(1, 2) with { Multicast = 1 };
            var right = Neighbor(4, 3);
            var result = Run([card, left, right], []);
            var growth = result.Events.OfType<CardAttributeChangedEvent>().Where(change => change.CardId == card.EntityId).ToArray();
            var hit = result.Events.OfType<DamageDealtEvent>().Single(change => change.SourceCardId == card.EntityId);
            if (growth.Length != 9 || growth.Any(change => change.AttributeKey != GameAttributeKeys.AttackDamage || change.Amount != bonus)
                || hit.RawDamage != damage + 6 * bonus || result.PermanentAttributeBonuses.Any()
                || result.PermanentChanges.Any()) return false;
            foreach (var excluded in new[]
            {
                left with { BoardStart = 0 },
                left with { Tags = new TagSet([GameTags.Beast]) },
                left with { IsOnBench = true },
            })
                if (Run([card, excluded], []).Events.OfType<CardAttributeChangedEvent>().Any(change => change.CardId == card.EntityId)) return false;
            if (Run([card], [left]).Events.OfType<CardAttributeChangedEvent>().Any(change => change.CardId == card.EntityId)
                || !solo.Events.SequenceEqual(Run([card], []).Events)
                || entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != damage) return false;
        }
        return true;
    }

    private static CardBattleSetup Neighbor(int start, int slots) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition("verification.spearman.neighbor", AbilityActivation.Active, AbilityTarget.SelfCard,
            0, 20, [new ArmorEffectDefinition(1)])], UseLegacyAttack: false,
        Tags: new TagSet([GameTags.Human]), OccupiedSlots: slots);

    private static BattleResult Run(CardBattleSetup[] cards, CardBattleSetup[] enemies) => new CombatSimulator().Simulate(new BattleSetup(
        new BattleSideSetup(new HeroBattleSetup(new EntityId(new System.Guid("00000000-0000-0000-0000-000000010001")), 10000, 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(new EntityId(new System.Guid("00000000-0000-0000-0000-000000010002")), 10000, 0, ManaRegen: 0), enemies),
        42, new BattleTick(62), new BattleTick(63)));
}
