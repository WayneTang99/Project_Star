using System.Threading.Tasks;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;
using System;
using System.Linq;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 冰火精灵双元素回响、排除自身、成长与充能验证（表现层）。
internal static class IcefireSpiritChecks
{
    internal static bool LevelsAndEchoes()
    {
        var definition = new IcefireSpiritCardDefinition();
        var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(IcefireSpiritChecks).Assembly);
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (!registry.Cards.ContainsKey(identity.Key) || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Small || !identity.ElementKeys.ToHashSet().SetEquals(new[] { GameElements.Ice, GameElements.Fire })
            || !definition.Tags.Contains(GameTags.Spirit) || definition.InitialLevel != 3 || definition.SupportsLevel(2)
            || definition.SupportsLevel(5) || image.GetWidth() * 2 != image.GetHeight() || !image.HasMipmaps()) return Fail();
        GD.Print($"冰火精灵原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var values = instance.Attributes.BaseCombat.SnapshotFinalValues();
            if (values[GameAttributeKeys.Burn] != 6 || values[GameAttributeKeys.CooldownTicks] != 80
                || instance.Abilities.Any(a => a.ManaCost != 0)) return Fail();
            var source = new CardBattleSetup(instance.Id, 0, 0, 80, instance.Abilities, UseLegacyAttack: false,
                Tags: instance.Tags, ElementKeys: identity.ElementKeys) { Level = level, CombatValues = values };
            var growth = level == 3 ? 4 : 8;
            foreach (var element in new[] { "none", "ice", "fire", "dual", "multi" })
            {
                var ice = element is "ice" or "dual" or "multi";
                var fire = element is "fire" or "dual" or "multi";
                var repeats = element == "multi" ? 3 : 1;
                var helper = Activator(ice && fire ? [GameElements.Ice, GameElements.Fire]
                    : ice ? [GameElements.Ice] : [GameElements.Fire]) with { Multicast = repeats - 1 };
                var enemies = Enumerable.Range(0, 3).Select(Target).ToArray();
                var bench = Target(4) with { IsOnBench = true };
                var dead = Target(5) with { Abilities = [new AbilityDefinition("verification.icefire.dead",
                    AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] };
                var setup = Setup(element == "none" ? [source] : [source, helper], enemies.Concat(new[] { bench, dead }).ToArray(), 161, reverse);
                var result = new CombatSimulator().Simulate(setup);
                var expectedBurn = 6 + (ice ? growth * repeats : 0);
                var first = fire ? 80 - 10 * repeats : 80;
                var burns = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.Burn && e.Amount > 0).ToArray();
                var charges = result.Events.OfType<CardChargedEvent>().Where(e => e.TargetCardId == source.EntityId).ToArray();
                var gains = result.Events.OfType<CardAttributeChangedEvent>().Where(e => e.CardId == source.EntityId).ToArray();
                if (burns.Length != 2 || burns.Any(e => e.Amount != expectedBurn)
                    || !burns.Select(e => e.Tick.Value).SequenceEqual(new long[] { first, first + 80 })
                    || charges.Length != (fire ? repeats : 0) || charges.Any(e => e.AmountTicks != 10)
                    || gains.Length != (ice ? repeats : 0) || result.PermanentAttributeBonuses.Any()
                    || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != 6
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return Fail();
                var frame = result.States.Last(s => s.Tick.Value == first);
                var held = frame.Cards.Where(c => c.Immobilize > 0).ToArray();
                if (held.Length != 1 || held[0].Immobilize != 10 || !enemies.Any(e => e.EntityId == held[0].Id)
                    || result.States.Last(s => s.Tick.Value == first + 10).Cards.Any(c => c.Immobilize > 0)) return Fail();
            }
            var dual = Activator([GameElements.Ice, GameElements.Fire]);
            foreach (var invalid in new[] { Activator([GameElements.Water]), dual with { IsOnBench = true } })
            {
                var result = Run([source, invalid], [], 80, reverse);
                if (result.Events.OfType<CardChargedEvent>().Any() || result.Events.OfType<CardAttributeChangedEvent>().Any()
                    || result.Events.OfType<StatusChangedEvent>().Single(e => e.Status == BattleStatus.Burn).Amount != 6) return Fail();
            }
            var enemyResult = Run([source], [dual], 80, reverse);
            if (enemyResult.Events.OfType<CardChargedEvent>().Any() || enemyResult.Events.OfType<CardAttributeChangedEvent>().Any()) return Fail();
            if (Run([source with { IsOnBench = true }, dual], [], 80, reverse).Events.OfType<StatusChangedEvent>().Any()) return Fail();
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var preview = new CardPreviewQuery(registry).Preview(offer, level == 3 ? 4 : 3, offer.GemSockets.Select(gem => gem?.Key).ToArray());
            if (offer.Abilities.Where(a => a.Activation == AbilityActivation.EchoOnMatchingAlliedCardActivated).Any(a => !a.ExcludeSourceCard)
                || preview.Abilities.Where(a => a.Activation == AbilityActivation.EchoOnMatchingAlliedCardActivated).Any(a => !a.ExcludeSourceCard)) return Fail();
        }
        return CardDescriptionChecks.AllLevels();
    }
    private static CardBattleSetup Activator(StringName[] elements) => new(EntityId.New(), 1, 0, 30,
        [new AbilityDefinition("verification.icefire.activate", AbilityActivation.Active, AbilityTarget.AlliedHero, 0, 30,
            [new RestoreManaEffectDefinition(0), new IncreaseSourceCooldownEffectDefinition(10000)])],
        UseLegacyAttack: false, ElementKeys: elements);
    private static CardBattleSetup Target(int start) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false);
    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse) =>
        new CombatSimulator().Simulate(Setup(allies, enemies, ticks, reverse));
    private static BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse)
    {
        var ally = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? enemy : ally, reverse ? ally : enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }
    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.Print($"冰火精灵验证失败，行 {line}"); return false; }
    internal static async Task Capture(Node owner)
    {
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new IcefireSpiritCardDefinition()));
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
            GD.Print($"冰火精灵原画 {artwork.GetWidth()}×{artwork.GetHeight()} mipmap={artwork.GetMipmapCount()}");
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(IcefireSpiritChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw();
            using var faceImage = viewport.GetTexture().GetImage();
            using var detailImage = owner.GetViewport().GetTexture().GetImage();
            faceImage.SavePng("res://output/icefire-spirit/card-face.png");
            detailImage.SavePng("res://output/icefire-spirit/preview.png");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }

}
