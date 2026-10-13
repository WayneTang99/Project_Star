using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 两种药水身份、随机目标、布尔状态与本场消耗验证（表现层）。
internal static class StatePotionChecks
{
    internal static bool LevelsAndConsumption()
    {
        var registry = DefinitionRegistry.Scan(typeof(BerserkPotionCardDefinition).Assembly);
        foreach (var definition in Definitions())
        {
            var identity = definition.Attributes.Identity;
            var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
            if (!registry.Cards.ContainsKey(identity.Key) || identity.FactionKey != new StringName("mona")
                || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
                || !definition.Tags.Contains(GameTags.Consumable) || definition.InitialLevel != 2
                || definition.SupportsLevel(1) || definition.SupportsLevel(5)
                || texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
            foreach (var level in new[] { 2, 3, 4 })
            foreach (var opposite in new[] { false, true })
            {
                var ability = definition.GetLevel(level)!.Abilities.Single();
                if (ability.CooldownTicks != 60 || ability.ManaCost != 0
                    || ability.Effects[0] is not SetRandomOtherAlliedCardStateEffectDefinition { Enabled: true }
                    || ability.Effects[1] is not DestroyCardEffectDefinition) return false;
                var potion = Potion(definition, level);
                var target = Fixture(4);
                var bench = Fixture(5) with { IsOnBench = true };
                var destroyed = Fixture(6) with { Abilities = [new AbilityDefinition(new StringName("ability.fixture_destroy"),
                    AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] };
                var enemy = Fixture(3);
                var setup = Setup([potion, target, bench, destroyed], [enemy], 60, opposite, 42);
                var simulator = new CombatSimulator();
                var result = simulator.Simulate(setup);
                var entry = result.Events.OfType<CardStateChangedEvent>().Single();
                var consumption = result.Events.OfType<CardDestroyedEvent>().Single(e => e.CardId == potion.EntityId);
                if (entry.CardId != target.EntityId || entry.Tick.Value != 60 || !entry.Enabled
                    || result.Events.ToList().IndexOf(entry) >= result.Events.ToList().IndexOf(consumption)
                    || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == potion.EntityId) != 1
                    || result.PermanentChanges.Count != 0 || !result.States[^1].Cards.Single(c => c.Id == potion.EntityId).Destroyed
                    || simulator.Simulate(setup).States[0].Cards.Single(c => c.Id == potion.EntityId).Destroyed
                    || simulator.Simulate(setup).States[0].Cards.Any(c => c.IsBerserk || c.IsFlying)) return false;
                var empty = simulator.Simulate(Setup([potion, bench, destroyed], [enemy], 60, opposite, 42));
                if (empty.Events.OfType<CardStateChangedEvent>().Any()
                    || !empty.States[^1].Cards.Single(c => c.Id == potion.EntityId).Destroyed) return false;
            }
        }
        return true;
    }

    internal static bool RandomAndStateEffects()
    {
        foreach (var definition in Definitions())
        {
            var potion = Potion(definition, 2);
            var first = Fixture(2); var second = Fixture(5);
            var selected = new System.Collections.Generic.HashSet<EntityId>();
            for (uint seed = 1; seed <= 20; seed++)
            {
                var setup = Setup([potion, first, second], [], 60, false, seed);
                var simulator = new CombatSimulator(); var result = simulator.Simulate(setup);
                var entry = result.Events.OfType<CardStateChangedEvent>().Single();
                selected.Add(entry.CardId);
                if (entry.CardId != first.EntityId && entry.CardId != second.EntityId
                    || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)
                    || result.States[0].Cards.Any(c => c.IsFlying || c.IsBerserk)) return false;
            }
            if (selected.Count != 2) return false;
            var attack = new AbilityDefinition(new StringName("ability.fixture_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 70, [new DamageEffectDefinition(100)]);
            var target = first with { Abilities = [attack] };
            var slow = new AbilityDefinition(new StringName("ability.fixture_slow"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 65, [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10)]);
            var enemy = Fixture(0) with { Abilities = [slow] };
            var combat = new CombatSimulator().Simulate(Setup([potion, target], [enemy], 80, false, 42));
            var state = combat.States.Last(s => s.Tick.Value == 65).Cards.Single(c => c.Id == target.EntityId);
            if (definition is FlyingPotionCardDefinition)
            {
                if (!state.IsFlying || state.Slow != 5) return false;
            }
            else if (!state.IsBerserk || combat.Events.OfType<DamageDealtEvent>().Single().RawDamage != 120) return false;
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(560, 560), TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var x = 35;
            foreach (var definition in Definitions())
            {
                var face = new CardItemView { Position = new Vector2(x, 35), Size = new Vector2(220, 440) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)), new CardDisplayAdapter());
                x += 265;
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/state-potions/card-faces.png") != Error.Ok)
                throw new InvalidOperationException("Potion capture failed");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardDefinition[] Definitions() => [new BerserkPotionCardDefinition(), new FlyingPotionCardDefinition()];
    private static CardBattleSetup Potion(CardDefinition definition, int level) =>
        new(EntityId.New(), 0, 0, 60, definition.GetLevel(level)!.Abilities, UseLegacyAttack: false, Multicast: 2);
    private static CardBattleSetup Fixture(int start) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false);
    private static BattleSetup Setup(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, bool opposite, uint seed) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), opposite ? enemies : cards),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), opposite ? cards : enemies),
            seed, new BattleTick(ticks), new BattleTick(ticks + 1));
}
