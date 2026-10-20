using Project_Star.Domain.Match;
using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Presentation.Playtest;
using System.Linq;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 烛台四级灼伤消耗、火属性回响疾速与过滤验证（表现层）。
internal static class CandelabrumChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new CandelabrumCardDefinition();
        var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (!DefinitionRegistry.Scan(typeof(CandelabrumChecks).Assembly).Cards.ContainsKey(identity.Key)
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Fire }) || definition.InitialLevel != 1
            || definition.SupportsLevel(5) || !definition.Tags.Contains(GameTags.Consumable)
            || image.GetWidth() * 2 != image.GetHeight() || !image.HasMipmaps()) return false;
        GD.Print($"烛台原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var values = instance.Attributes.BaseCombat.SnapshotFinalValues();
            var burn = 10 << (level - 1);
            if (values[GameAttributeKeys.Burn] != burn || values[GameAttributeKeys.CooldownTicks] != 100
                || instance.Abilities.Any(a => a.ManaCost != 0)) return false;
            var candle = new CardBattleSetup(instance.Id, 0, 0, 100, instance.Abilities, UseLegacyAttack: false,
                Tags: instance.Tags, ElementKeys: identity.ElementKeys) { Level = level, CombatValues = values };
            var setup = Setup([candle with { Multicast = 2 }], [], 210, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var burns = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.Burn && e.Amount > 0).ToArray();
            var destroyed = result.Events.OfType<CardDestroyedEvent>().ToArray();
            if (burns.Length != 1 || burns[0].Amount != burn || burns[0].Tick.Value != 100
                || destroyed.Length != 1 || destroyed[0].CardId != candle.EntityId || destroyed[0].Tick.Value != 100
                || result.Events.ToList().IndexOf(burns[0]) >= result.Events.ToList().IndexOf(destroyed[0])
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == candle.EntityId) != 1
                || result.Events.OfType<DamageDealtEvent>().Any(e => e.SourceCardId == candle.EntityId && e.SourceKind == DamageSourceKind.Card)
                || result.PermanentChanges.Any() || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var fire = Activator(GameElements.Fire);
            var accelerated = Run([candle, fire], [], 100, reverse);
            var haste = accelerated.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.HasteDuration).ToArray();
            if (haste.Length != 3 || haste.Any(e => e.Amount != 10)
                || !haste.Select(e => e.Tick.Value).SequenceEqual(new long[] { 20, 40, 60 })
                || accelerated.Events.OfType<CardDestroyedEvent>().Single().Tick.Value != 70) return false;
            var multi = Run([candle, fire with { Multicast = 2 }], [], 25, reverse);
            if (multi.Events.OfType<StatusChangedEvent>().Count(e => e.Status == BattleStatus.HasteDuration && e.Amount == 10) != 3) return false;
            foreach (var invalid in new[] { Activator(GameElements.Water), fire with { IsOnBench = true } })
                if (Run([candle, invalid], [], 99, reverse).Events.OfType<StatusChangedEvent>().Any(e => e.Status == BattleStatus.HasteDuration)) return false;
            if (Run([candle], [fire], 99, reverse).Events.OfType<StatusChangedEvent>().Any(e => e.Status == BattleStatus.HasteDuration)
                || Run([candle with { IsOnBench = true }, fire], [], 110, reverse).Events.OfType<CardDestroyedEvent>().Any()) return false;
        }
        var rules = string.Join("\n", CardDisplayAdapter.Rules(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition))));
        return rules.Contains("消耗") && !rules.Contains("本场") && !rules.Contains("摧毁") && CardDescriptionChecks.AllLevels();
    }

    private static CardBattleSetup Activator(StringName element) => new(EntityId.New(), 1, 0, 20,
        [new AbilityDefinition("ability.verification.candle_trigger", AbilityActivation.Active, AbilityTarget.AlliedHero,
            0, 20, [new RestoreManaEffectDefinition(0)])], UseLegacyAttack: false, ElementKeys: [element]);
    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse) =>
        new CombatSimulator().Simulate(Setup(allies, enemies, ticks, reverse));
    private static BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse)
    {
        var ally = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? enemy : ally, reverse ? ally : enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }
    internal static async Task Capture(Node owner)
    {
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new CandelabrumCardDefinition()));
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
            GD.Print($"烛台原画 {artwork.GetWidth()}×{artwork.GetHeight()} mipmap={artwork.GetMipmapCount()}");
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(CandelabrumChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw();
            using var faceImage = viewport.GetTexture().GetImage();
            using var detailImage = owner.GetViewport().GetTexture().GetImage();
            faceImage.SavePng("res://output/candelabrum/card-face.png");
            detailImage.SavePng("res://output/candelabrum/preview.png");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }

}
