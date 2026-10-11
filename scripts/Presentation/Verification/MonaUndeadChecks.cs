using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 骷髅分级伤害及骨冢周期召唤验证（表现层验证模块）。
internal static class MonaUndeadChecks
{
    internal static bool LevelsAndDamage()
    {
        var registry = DefinitionRegistry.Scan(typeof(SkeletonCardDefinition).Assembly);
        foreach (var definition in new CardDefinition[] { new SkeletonCardDefinition(), new BoneBarrowCardDefinition() })
        {
            var skeleton = definition is SkeletonCardDefinition;
            var identity = definition.Attributes.Identity;
            if (!registry.Cards.ContainsKey(identity.Key)
                || identity.Key != new StringName(skeleton ? "card.skeleton" : "card.bone_barrow")
                || identity.DisplayName != (skeleton ? "骷髅" : "骨冢")
                || identity.FactionKey != new StringName("mona")
                || identity.Size != (skeleton ? CardSize.Small : CardSize.Medium)
                || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
                || !definition.Tags.Contains(GameTags.Undead)
                || !skeleton && !definition.Tags.Contains(GameTags.Location)
                || definition.InitialLevel != (skeleton ? 1 : 2)
                || definition.SupportsLevel(5) || !skeleton && definition.SupportsLevel(1)
                || identity.DescriptionEntries.Single().KeywordKey != CardKeywords.Activate) return false;
            var texture = ResourceLoader.Load<Texture2D>(identity.Illustration.ToString());
            if (texture is null || texture.GetHeight() != texture.GetWidth() * (skeleton ? 2 : 1)) return false;
            for (var level = definition.InitialLevel; level <= 4; level++)
            {
                var card = new EntityFactory().CreateCard(definition, level);
                var ability = card.Abilities.Single();
                if (ability.Activation != AbilityActivation.Active || ability.CooldownTicks != 30 || ability.ManaCost != 0
                    || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 30) return false;
                if (!skeleton) continue;
                if (card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != level * 10) return false;
                var source = Card(definition, level, 0);
                var result = new CombatSimulator().Simulate(Battle([source], [], 60));
                var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == source.EntityId).ToArray();
                if (!hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 30, 60 })
                    || hits.Any(hit => hit.RawDamage != level * 10) || result.Events.OfType<ManaChangedEvent>().Any()) return false;
            }
        }
        return true;
    }

    internal static bool SummonTimingAndPositions()
    {
        for (var level = 2; level <= 4; level++)
        {
            var source = Card(new BoneBarrowCardDefinition(), level, 3);
            var setup = Battle([source], [], 90);
            var result = new CombatSimulator().Simulate(setup);
            var summons = result.Events.OfType<CardSummonedEvent>().ToArray();
            if (summons.Length != 1) return false;
            var summon = summons[0];
            var state = result.States[^1].Cards.Single(card => card.Id == summon.SummonedCardId);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == summon.SummonedCardId).ToArray();
            if (summon.Tick.Value != 30 || summon.BoardStart != 2 || summon.Level != level
                || summon.CardKey != new StringName("card.skeleton") || summon.SourceCardId != source.EntityId
                || state.SummonedCard?.Identity.DisplayName != "骷髅" || state.Level != level
                || !state.SummonedCard.Tags.Contains(GameTags.Undead)
                || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 60, 90 })
                || hits.Any(hit => hit.RawDamage != level * 10)
                || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == source.EntityId) != 3
                || result.Events.OfType<ManaChangedEvent>().Any() || result.PermanentChanges.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        }
        var barrow = Card(new BoneBarrowCardDefinition(), 2, 3);
        var blocker = new CardBattleSetup(EntityId.New(), 1, 0, 0, [], UseLegacyAttack: false, OccupiedSlots: 2);
        if (!Starts(Battle([barrow], [], 29))
            || !Starts(Battle([barrow with { BoardStart = 0 }], [], 60))
            || !Starts(Battle([barrow, blocker], [], 60))
            || !Starts(Battle([barrow with { IsOnBench = true }], [], 60))
            || !Starts(Battle([barrow, blocker with { IsOnBench = true }], [blocker], 30), 2)
            || !Starts(Battle([barrow with { BoardStart = 1 }], [], 30), 0)) return false;
        var enemy = new CombatSimulator().Simulate(Battle([], [barrow], 60));
        return enemy.Events.OfType<CardSummonedEvent>().Single().Side == SideId.Opponent
            && enemy.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceKind == DamageSourceKind.Card).RawDamage == 20;
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, start, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            30, card.Abilities, UseLegacyAttack: false, Tags: card.Tags,
            OccupiedSlots: definition.Attributes.Identity.OccupiedSlots,
            ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level };
    }

    private static bool Starts(BattleSetup setup, params int[] starts) => new CombatSimulator().Simulate(setup).Events
        .OfType<CardSummonedEvent>().Select(item => item.BoardStart).SequenceEqual(starts);

    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] enemy, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), player),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), enemy),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
