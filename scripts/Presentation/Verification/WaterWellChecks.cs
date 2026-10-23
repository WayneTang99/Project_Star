using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 水井分级治疗、灼伤扣减边界、周期、多重与原画验证（表现层）。
internal static class WaterWellChecks
{
    internal static bool LevelsAndReduction()
    {
        var definition = new WaterWellCardDefinition(); var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (identity.DisplayName != "水井" || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Water }) || definition.InitialLevel != 1
            || definition.SupportsLevel(0) || definition.SupportsLevel(5) || definition.Tags.Count != 1
            || !definition.Tags.Contains(GameTags.Small) || image.GetHeight() != image.GetWidth() * 2 || !image.HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(WaterWellChecks).Assembly).Cards.ContainsKey(identity.Key)) return Fail();
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        foreach (var burn in new[] { 0, 2, 100 })
        foreach (var multicast in new[] { 0, 1 })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var healing = 10 * (1 << (level - 1)); var reduction = 4 * (1 << (level - 1));
            var source = new CardBattleSetup(card.Id, 0, 0, 60, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, Multicast: multicast, ElementKeys: identity.ElementKeys)
                { Level = level, Identity = identity, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
            if (card.Abilities.Single().ManaCost != 0 || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 60) return Fail();
            var setup = Battle(source, reverse, burn, 64);
            var result = new CombatSimulator().Simulate(setup);
            var after = result.States.Last(s => s.Tick.Value == 63);
            var own = reverse ? after.Opponent : after.Player;
            var other = reverse ? after.Player : after.Opponent;
            var pulses = multicast + 1;
            var expectedRemoved = Math.Min(Math.Max(0, burn - 1), reduction * pulses);
            var reductions = result.Events.OfType<StatusChangedEvent>().Where(e => e.Amount < 0).ToArray();
            if (own.Burn != Math.Max(0, burn - 1 - reduction * pulses)
                || own.Health != 9000 - burn + healing * pulses || own.Poison != 0 || other.Burn != 0
                || reductions.Sum(e => -e.Amount) != expectedRemoved || reductions.Any(e => e.Status != BattleStatus.Burn)
                || !result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == card.Id).Select(e => e.Tick.Value)
                    .SequenceEqual(multicast == 0 ? new long[] { 60 } : new long[] { 60, 62 })
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()) return Fail();
            var cycle = new CombatSimulator().Simulate(Battle(source with { Multicast = 0 }, reverse, 100, 124));
            if (!cycle.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == card.Id).Select(e => e.Tick.Value)
                .SequenceEqual(new long[] { 60, 120 })
                || cycle.Events.OfType<StatusChangedEvent>().Count(e => e.Amount == -reduction) != 2) return Fail();
            foreach (var inactive in new[] { source with { IsOnBench = true }, source with { Abilities = [.. source.Abilities!, Destroy()] } })
                if (new CombatSimulator().Simulate(Battle(inactive, reverse, 100, 64)).Events.OfType<StatusChangedEvent>().Any(e => e.Amount < 0)) return Fail();
        }
        return true;
    }

    private static AbilityDefinition Destroy() => new(new StringName("ability.verification_destroy"), AbilityActivation.PassiveOnBattleStart,
        AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);

    private static BattleSetup Battle(CardBattleSetup source, bool reverse, int burn, int ticks)
    {
        var enemy = new CardBattleSetup(EntityId.New(), 0, 0, 59,
            [new AbilityDefinition(new StringName("ability.verification_damage"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(1000)]),
             new AbilityDefinition(new StringName("ability.verification_burn"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 59, [new ApplyStatusEffectDefinition(BattleStatus.Burn, burn)])], UseLegacyAttack: false);
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [source]);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [enemy]);
        return new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(620, 450), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(620, 450) });
            using var artwork = GD.Load<Texture2D>(new WaterWellCardDefinition().Attributes.Identity.Illustration.ToString()).GetImage();
            GD.Print($"水井原画 {artwork.GetWidth()}×{artwork.GetHeight()} mipmaps={artwork.GetMipmapCount()}");
            foreach (var (level, x, width) in new[] { (1, 20, 200), (4, 240, 200), (1, 470, 100) })
            {
                var face = new CardItemView { Position = new Vector2(x, 20), Size = new Vector2(width, width * 2) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new WaterWellCardDefinition(), level)), new CardDisplayAdapter());
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/water-well/card-face.png") != Error.Ok) throw new InvalidOperationException("水井截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.PushError("水井验证失败，行 " + line); return false; }
}
