using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Presentation.Game;
using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 骷髅巨人三级伤害、16秒冷却与己方小型摧毁充能验证（表现层）。
internal static class SkeletonGiantChecks
{
    internal static bool DamageAndCharge()
    {
        var definition = new SkeletonGiantCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "骷髅巨人" || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Large || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Large, GameTags.Undead })
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(SkeletonGiantChecks).Assembly).Cards.ContainsKey(identity.Key)) return Fail();
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var instance = new EntityFactory().CreateCard(definition, level); var amount = level == 2 ? 240 : level == 3 ? 480 : 980;
            var giant = new CardBattleSetup(instance.Id, 6, amount, 160, instance.Abilities, UseLegacyAttack: false,
                OccupiedSlots: 3, Tags: instance.Tags, ElementKeys: identity.ElementKeys) { Level = level };
            foreach (var count in new[] { 0, 1, 2 })
            foreach (var element in new[] { GameElements.General, GameElements.Dark, GameElements.Wind })
            {
                var victims = Enumerable.Range(0, count).Select(i => Victim(i, 1, element)).ToArray();
                var setup = Setup([giant, .. victims], [], reverse);
                var result = new CombatSimulator().Simulate(setup);
                var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == giant.EntityId).ToArray();
                var echoes = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == giant.EntityId && e.IsEcho).ToArray();
                if (hits.Length != 1 || hits[0].Tick.Value != 160 - count * 10 || hits[0].RawDamage != amount
                    || echoes.Length != count || echoes.Any(e => e.Tick.Value != 10)
                    || result.Events.OfType<CardChargedEvent>().Count() != count
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                    || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()) return Fail();
            }
            foreach (var excluded in new[] { Victim(0, 2, GameElements.General), Victim(0, 3, GameElements.General),
                Victim(0, 1, GameElements.General) with { IsOnBench = true } })
                if (new CombatSimulator().Simulate(Setup([giant, excluded], [], reverse)).Events.OfType<CardChargedEvent>().Any()) return Fail();
            if (new CombatSimulator().Simulate(Setup([giant], [Victim(0, 1, GameElements.General)], reverse))
                .Events.OfType<CardChargedEvent>().Any()) return Fail();
            var echoVictim = Victim(0, 1, GameElements.Dark) with { Abilities =
                [new AbilityDefinition(new StringName("verification.giant.active"), AbilityActivation.Active, AbilityTarget.AlliedHero,
                    0, 10, [new RestoreManaEffectDefinition(0)]),
                 new AbilityDefinition(new StringName("verification.giant.echo_destroy"), AbilityActivation.EchoOnSourceCardActivated,
                    AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] };
            if (new CombatSimulator().Simulate(Setup([giant, echoVictim], [], reverse)).Events.OfType<CardChargedEvent>().Any()) return Fail();
            foreach (var inactive in new[] { giant with { IsOnBench = true }, giant with { Abilities = giant.Abilities!.Append(
                new AbilityDefinition(new StringName("verification.giant.destroy_source"), AbilityActivation.PassiveOnBattleStart,
                    AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() } })
            {
                var result = new CombatSimulator().Simulate(Setup([inactive, Victim(0, 1, GameElements.General)], [], reverse));
                if (result.Events.OfType<CardChargedEvent>().Any() || result.Events.OfType<DamageDealtEvent>().Any()) return Fail();
            }
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.FaceEffects(offer).Contains(new CardFaceEffect(CardFaceEffectKind.Damage, amount.ToString()))
                || CardDisplayAdapter.CooldownSeconds(offer) != 16m
                || !CardDisplayAdapter.Rules(offer).Any(rule => rule.Contains("小型") && rule.Contains("充能 1秒"))) return Fail();
        }
        return true;
    }

    private static CardBattleSetup Victim(int start, int slots, StringName element) => new(EntityId.New(), start, 0, 10,
        [new AbilityDefinition(new StringName("verification.giant.consume"), AbilityActivation.Active, AbilityTarget.SelfCard,
            0, 10, [new DestroyCardEffectDefinition(false)])], UseLegacyAttack: false, OccupiedSlots: slots, ElementKeys: [element]);
    private static BattleSetup Setup(CardBattleSetup[] own, CardBattleSetup[] enemy, bool reverse) => new(
        new BattleSideSetup(new HeroBattleSetup(new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000001")), 10000, 0, ManaRegen: 0), reverse ? enemy : own),
        new BattleSideSetup(new HeroBattleSetup(new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000002")), 10000, 0, ManaRegen: 0), reverse ? own : enemy),
        42, new BattleTick(165), new BattleTick(166));
    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.Print($"骷髅巨人验证失败，行{line}"); return false; }
    internal static async Task Capture(Node owner)
    {
        if (!DamageAndCharge()) throw new InvalidOperationException("骷髅巨人战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new SkeletonGiantCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() * 2 != raw.GetHeight() * 3)
            throw new InvalidOperationException("骷髅巨人原画比例或mipmap不合格。");
        GD.Print($"骷髅巨人原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
        var viewport = new SubViewport { Size = new Vector2I(550, 280), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(550, 280) });
            foreach (var (position, size) in new[] { (new Vector2(24, 24), new Vector2(300, 200)),
                (new Vector2(360, 24), new Vector2(150, 100)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, adapter);
            }
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(SkeletonGiantChecks).Assembly)));
            preview.SetEntries(CardCatalogQuery.Capture(DefinitionRegistry.Scan(typeof(SkeletonGiantChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/skeleton-giant/card-face.png") != Error.Ok
                || detail.SavePng("res://output/skeleton-giant/preview.png") != Error.Ok)
                throw new InvalidOperationException("骷髅巨人卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
