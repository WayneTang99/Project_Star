using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 火焰提灯分级随机目标、迟缓到期与固定灼伤验证（表现层）。
internal static class FlameLanternChecks
{
    internal static bool LevelsAndTargets()
    {
        var definition = new FlameLanternCardDefinition();
        var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (!DefinitionRegistry.Scan(typeof(FlameLanternChecks).Assembly).Cards.ContainsKey(identity.Key)
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Fire }) || definition.Tags.Count != 1
            || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || image.GetWidth() * 2 != image.GetHeight() || !image.HasMipmaps()) return Fail();
        GD.Print($"火焰提灯原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        foreach (var count in new[] { 0, 2, 5 })
        foreach (var seed in new ulong[] { 1, 42, 73 })
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var values = instance.Attributes.BaseCombat.SnapshotFinalValues();
            if (values[GameAttributeKeys.Burn] != 2 || values[GameAttributeKeys.CooldownTicks] != 40
                || instance.Abilities.Single().ManaCost != 0) return Fail();
            var source = new CardBattleSetup(instance.Id, 0, 0, 40, instance.Abilities, UseLegacyAttack: false,
                Tags: instance.Tags, ElementKeys: identity.ElementKeys) { Level = level, CombatValues = values };
            var targets = Enumerable.Range(0, count).Select(Target).ToArray();
            var bench = Target(7) with { IsOnBench = true };
            var dead = Target(8) with { Abilities = [new AbilityDefinition("verification.lantern.destroy",
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] };
            var ally = Target(1);
            var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [source, ally]);
            var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), targets.Concat(new[] { bench, dead }).ToArray());
            var setup = new BattleSetup(reverse ? enemy : own, reverse ? own : enemy, seed, new BattleTick(80), new BattleTick(81));
            var result = new CombatSimulator().Simulate(setup);
            var frame = result.States.Last(s => s.Tick.Value == 40);
            var slowed = frame.Cards.Where(card => card.Slow > 0).ToArray();
            var burns = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.Burn && e.Amount > 0).ToArray();
            if (slowed.Length != System.Math.Min(level, count) || slowed.Any(c => c.Slow != 10 || !targets.Any(t => t.EntityId == c.Id))
                || burns.Length != 2 || burns.Any(e => e.Amount != 2)
                || !burns.Select(e => e.Tick.Value).SequenceEqual(new long[] { 40, 80 })
                || result.States.Last(s => s.Tick.Value == 50).Cards.Any(c => c.Slow > 0)
                || frame.Cards.Any(c => new[] { source.EntityId, ally.EntityId, bench.EntityId, dead.EntityId }.Contains(c.Id) && c.Slow > 0)
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return Fail();
            var inactive = source with { IsOnBench = true };
            var inactiveSide = new BattleSideSetup(own.Hero, [inactive]);
            var inactiveResult = new CombatSimulator().Simulate(new BattleSetup(reverse ? enemy : inactiveSide,
                reverse ? inactiveSide : enemy, seed, new BattleTick(80), new BattleTick(81)));
            if (inactiveResult.Events.OfType<StatusChangedEvent>().Any()) return Fail();
        }
        return CardDescriptionChecks.AllLevels();
    }
    private static CardBattleSetup Target(int start) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false);
    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.Print($"火焰提灯验证失败，行 {line}"); return false; }
    internal static async Task Capture(Node owner)
    {
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new FlameLanternCardDefinition()));
        var viewport = new SubViewport { Size = new Vector2I(650, 440), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(650, 440) });
            foreach (var (position, size) in new[] { (new Vector2(20, 20), new Vector2(200, 400)),
                (new Vector2(460, 20), new Vector2(100, 200)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, new CardDisplayAdapter());
            }
            using var artwork = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage();
            GD.Print($"火焰提灯原画 {artwork.GetWidth()}×{artwork.GetHeight()} mipmap={artwork.GetMipmapCount()}");
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(FlameLanternChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw();
            using var faceImage = viewport.GetTexture().GetImage();
            using var detailImage = owner.GetViewport().GetTexture().GetImage();
            faceImage.SavePng("res://output/flame-lantern/card-face.png");
            detailImage.SavePng("res://output/flame-lantern/preview.png");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }

}
