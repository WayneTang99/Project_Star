using System;
using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 南瓜灯四级、随机迟缓和相邻发动再生验证（表现层）。
internal static class PumpkinLanternChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new PumpkinLanternCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "南瓜灯" || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Medium || definition.InitialLevel != 1
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Wood, GameElements.Dark })
            || definition.Tags.Count != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(PumpkinLanternChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        foreach (var count in new[] { 0, 1, 4 })
        foreach (var seed in new ulong[] { 1, 42 })
        foreach (var inactive in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var displayed = CardDisplayAdapter.FaceEffects(offer).Where(e => e.Kind == CardFaceEffectKind.HealthRegen).ToArray();
            if (displayed.Length != 1 || displayed[0].Value != (2 << (level - 1)).ToString()) return false;
            var source = new CardBattleSetup(card.Id, 3, 0, 50, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, ElementKeys: identity.ElementKeys, OccupiedSlots: 2, IsOnBench: inactive)
                { Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
            var left = Neighbor(0, 3) with { Multicast = 1 };
            var right = Neighbor(5, 1);
            var distant = Neighbor(7, 1);
            var bench = Neighbor(2, 1) with { IsOnBench = true };
            var targets = Enumerable.Range(0, count).Select(i => new CardBattleSetup(EntityId.New(), i,
                0, 0, [], UseLegacyAttack: false)).ToArray();
            var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0),
                [source, left, right, distant, bench]);
            var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0),
                [.. targets, Neighbor(8, 1) with { IsOnBench = true }]);
            var setup = new BattleSetup(reverse ? enemy : own, reverse ? own : enemy,
                seed, new BattleTick(61), new BattleTick(62));
            var result = new CombatSimulator().Simulate(setup);
            var frame = result.States.Last(s => s.Tick.Value == 50);
            var hero = reverse ? frame.Opponent : frame.Player;
            var other = reverse ? frame.Player : frame.Opponent;
            var slowed = frame.Cards.Where(c => c.Slow > 0).ToArray();
            // 左侧两次正常发动及两次多重、右侧两次发动，共六次回响；隔空、敌方及备战不触发。
            if (hero.HealthRegen != (inactive ? 0 : 6 * (2 << (level - 1))) || other.HealthRegen != 0
                || slowed.Length != (inactive ? 0 : System.Math.Min(2, count))
                || slowed.Any(c => c.Slow != 10 || !targets.Any(t => t.EntityId == c.Id))
                || result.States.Last(s => s.Tick.Value == 60).Cards.Any(c => c.Slow > 0)
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == card.Id && !e.IsEcho) != (inactive ? 0 : 1)
                || result.PermanentAttributeBonuses.Any() || result.PermanentChanges.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events))
            { GD.Print($"南瓜灯失败 level={level},reverse={reverse},count={count},inactive={inactive},regen={hero.HealthRegen}"); return false; }
        }
        return true;
    }

    private static CardBattleSetup Neighbor(int start, int slots) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition(new StringName("verification.pumpkin_neighbor"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 20, [new RestoreManaEffectDefinition(1)])],
        UseLegacyAttack: false, OccupiedSlots: slots);
    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCombat()) throw new InvalidOperationException("南瓜灯战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new PumpkinLanternCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() != raw.GetHeight())
            throw new InvalidOperationException("南瓜灯原画比例或mipmap不合格。");
        GD.Print($"南瓜灯原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
        var viewport = new SubViewport { Size = new Vector2I(420, 460), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(420, 460) });
            foreach (var (position, size) in new[] { (new Vector2(24, 24), new Vector2(200, 200)),
                (new Vector2(260, 24), new Vector2(100, 100)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, adapter);
            }
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(PumpkinLanternChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/pumpkin-lantern/card-face.png") != Error.Ok
                || detail.SavePng("res://output/pumpkin-lantern/preview.png") != Error.Ok)
                throw new InvalidOperationException("南瓜灯卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
