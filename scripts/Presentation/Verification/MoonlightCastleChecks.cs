using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;
using System;
using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 月光古堡身份、暗属性多重叠加及来源失效验证（表现层）。
internal static class MoonlightCastleChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new MoonlightCastleCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "月光古堡" || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Large || definition.InitialLevel != 4
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Large, GameTags.Building, GameTags.Location })
            || Enumerable.Range(0, 6).Any(l => definition.SupportsLevel(l) != (l == 4))
            || !DefinitionRegistry.Scan(typeof(MoonlightCastleChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var abilities = definition.GetLevel(4)!.Abilities;
        if (abilities.Count != 1 || abilities[0].Activation != AbilityActivation.PassiveAura
            || abilities[0].CooldownTicks != 0 || abilities[0].ManaCost != 0) return false;
        foreach (var reverse in new[] { false, true })
        foreach (var count in new[] { 0, 1, 2 })
        foreach (var destroyed in new[] { false, true })
        {
            var destroy = new AbilityDefinition(new StringName("verification.castle_destroy"),
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);
            var sources = Enumerable.Range(0, count).Select(i => new CardBattleSetup(EntityId.New(), i * 3, 0, 0,
                destroyed ? [.. abilities, destroy] : abilities, UseLegacyAttack: false, OccupiedSlots: 3,
                ElementKeys: [GameElements.Dark])).ToArray();
            var dark = Attacker(6, [GameElements.Dark, GameElements.Wood]);
            var general = Attacker(7, [GameElements.General]);
            var enemyCard = Attacker(0, [GameElements.Dark]);
            var bench = new CardBattleSetup(EntityId.New(), 0, 0, 0, abilities, IsOnBench: true,
                UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Dark]);
            var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [.. sources, dark, general, bench]);
            var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [enemyCard]);
            var setup = new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(15), new BattleTick(16));
            var result = new CombatSimulator().Simulate(setup);
            int Hits(EntityId id) => result.Events.OfType<DamageDealtEvent>().Count(e => e.SourceCardId == id);
            if (Hits(dark.EntityId) != 1 + (destroyed ? 0 : count) || Hits(general.EntityId) != 1 || Hits(enemyCard.EntityId) != 1
                || result.PermanentAttributeBonuses.Any() || result.PermanentChanges.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        }
        return true;
    }

    private static CardBattleSetup Attacker(int start, StringName[] elements) => new(EntityId.New(), start, 1, 10,
        [new AbilityDefinition(new StringName("verification.castle_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 10, [new DamageEffectDefinition(1)])], UseLegacyAttack: false, ElementKeys: elements);
    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCombat()) throw new InvalidOperationException("月光古堡战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new MoonlightCastleCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() * 2 != raw.GetHeight() * 3)
            throw new InvalidOperationException("月光古堡原画比例或mipmap不合格。");
        GD.Print($"月光古堡原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
        var viewport = new SubViewport { Size = new Vector2I(550, 280), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(420, 460) });
            foreach (var (position, size) in new[] { (new Vector2(24, 24), new Vector2(300, 200)),
                (new Vector2(360, 24), new Vector2(150, 100)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, adapter);
            }
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(MoonlightCastleChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/moonlight-castle/card-face.png") != Error.Ok
                || detail.SavePng("res://output/moonlight-castle/preview.png") != Error.Ok)
                throw new InvalidOperationException("月光古堡卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
