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

// 死灵禁术师非暗小型筛选、来源等级召唤与回响生命周期验证（表现层）。
internal static class UndeadOccultistChecks
{
    internal static bool SummonsAndFilters()
    {
        var definition = new UndeadOccultistCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "死灵禁术师" || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Medium || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Medium, GameTags.Undead })
            || definition.InitialLevel != 4 || definition.SupportsLevel(3) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(UndeadOccultistChecks).Assembly).Cards.ContainsKey(identity.Key)) return Fail();
        var instance = new EntityFactory().CreateCard(definition, 4);
        var source = new CardBattleSetup(instance.Id, 5, 0, 0, instance.Abilities, UseLegacyAttack: false,
            OccupiedSlots: 2, ElementKeys: identity.ElementKeys, Tags: instance.Tags) { Level = 4 };
        foreach (var reverse in new[] { false, true })
        foreach (var level in new[] { 1, 2, 3, 4 })
        foreach (var elements in new StringName[][] { [GameElements.General], [GameElements.Fire, GameElements.Wood],
            [GameElements.Dark], [GameElements.Fire, GameElements.Dark] })
        foreach (var slots in new[] { 1, 2, 3 })
        {
            var victim = Victim(level, slots, elements); var setup = Setup([source, victim], [], 45, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var summons = result.Events.OfType<CardSummonedEvent>().ToArray();
            var expected = slots == 1 && !elements.Contains(GameElements.Dark);
            if (summons.Length != (expected ? 1 : 0)) return Fail();
            if (expected)
            {
                var summoned = summons.Single();
                var hit = result.Events.OfType<DamageDealtEvent>().Single(e => e.SourceCardId == summoned.SummonedCardId);
                if (summoned.CardKey != new StringName("card.skeleton") || summoned.BoardStart != 0 || summoned.Level != 4
                    || summoned.SourceCardId != source.EntityId || hit.RawDamage != 40 || hit.Tick.Value != 40) return Fail();
            }
            if (!result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()) return Fail();
        }
        var small = Victim(1, 1, [GameElements.General]);
        var dead = source with { Abilities = source.Abilities!.Append(Destroy(0)).ToArray() };
        foreach (var inactive in new[] { source with { IsOnBench = true }, dead })
            if (Run([inactive, small], [], 45).Events.OfType<CardSummonedEvent>().Any()) return Fail();
        if (Run([source], [small], 45).Events.OfType<CardSummonedEvent>().Any()
            || Run([source, small with { IsOnBench = true }], [], 45).Events.OfType<CardSummonedEvent>().Any()) return Fail();
        var echoVictim = small with { Abilities = [new AbilityDefinition(new StringName("verification.occultist.active"),
            AbilityActivation.Active, AbilityTarget.AlliedHero, 0, 10, [new RestoreManaEffectDefinition(0)]),
            new AbilityDefinition(new StringName("verification.occultist.echo_destroy"), AbilityActivation.EchoOnSourceCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] };
        if (Run([source, echoVictim], [], 45).Events.OfType<CardSummonedEvent>().Any()) return Fail();
        var doubleSource = source with { EntityId = EntityId.New(), BoardStart = 7 };
        if (Run([source, doubleSource, small], [], 45).Events.OfType<CardSummonedEvent>().Count() != 1) return Fail();
        var sweeper = new CardBattleSetup(EntityId.New(), 0, 0, 20,
            [new AbilityDefinition(new StringName("verification.occultist.sweep"), AbilityActivation.Active, AbilityTarget.SelfCard,
                0, 20, [new DestroyBattlefieldCardsEffectDefinition(CardSize.Small)])], UseLegacyAttack: false);
        if (Run([source, small], [sweeper], 45).Events.OfType<CardSummonedEvent>().Count() != 1) return Fail();
        var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, 4));
        if (offer.Abilities.Single().ExcludedTriggerCardElement != GameElements.Dark
            || !CardDisplayAdapter.Rules(offer).Single().Contains("小型非暗属性")
            || !CardDisplayAdapter.Rules(offer).Single().Contains("与此卡牌同级")
            || new CardDisplayAdapter().Build(offer).Effects.Count != 0
            || CardDisplayAdapter.CurrentDetails(offer).Contains("冷却")) return Fail();
        return RebirthRitualChecks.SummonsAndFilters();
    }

    private static CardBattleSetup Victim(int level, int slots, StringName[] elements) => new(EntityId.New(), 0, 0, 10,
        [Destroy(10)], UseLegacyAttack: false, OccupiedSlots: slots, ElementKeys: elements) { Level = level };
    private static AbilityDefinition Destroy(int cooldown) => new(new StringName("verification.occultist.destroy"),
        cooldown == 0 ? AbilityActivation.PassiveOnBattleStart : AbilityActivation.Active, AbilityTarget.SelfCard,
        0, cooldown, [new DestroyCardEffectDefinition(false)]);
    private static BattleSetup Setup(CardBattleSetup[] own, CardBattleSetup[] enemy, int ticks, bool reverse = false) => new(
        new BattleSideSetup(new HeroBattleSetup(new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000001")), 10000, 0, ManaRegen: 0), reverse ? enemy : own),
        new BattleSideSetup(new HeroBattleSetup(new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000002")), 10000, 0, ManaRegen: 0), reverse ? own : enemy),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
    private static BattleResult Run(CardBattleSetup[] own, CardBattleSetup[] enemy, int ticks) => new CombatSimulator().Simulate(Setup(own, enemy, ticks));
    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.Print($"死灵禁术师验证失败，行{line}"); return false; }
    internal static async Task Capture(Node owner)
    {
        if (!SummonsAndFilters()) throw new InvalidOperationException("死灵禁术师战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new UndeadOccultistCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() != raw.GetHeight())
            throw new InvalidOperationException("死灵禁术师原画比例或mipmap不合格。");
        GD.Print($"死灵禁术师原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
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
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(UndeadOccultistChecks).Assembly)));
            preview.SetEntries(CardCatalogQuery.Capture(DefinitionRegistry.Scan(typeof(UndeadOccultistChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/undead-occultist/card-face.png") != Error.Ok
                || detail.SavePng("res://output/undead-occultist/preview.png") != Error.Ok)
                throw new InvalidOperationException("死灵禁术师卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
