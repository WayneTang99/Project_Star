using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Content;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

internal static class SpiderQueenChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new SpiderQueenCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "蜘蛛女皇" || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Large || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Wood })
            || !definition.Tags.Contains(GameTags.Insect) || definition.InitialLevel != 3 || definition.SupportsLevel(2)) return Fail("identity");
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var queen = Card(definition, level, 2);
            var cooldown = level == 3 ? 90 : 80;
            if (queen.Abilities!.Single(a => a.Activation == AbilityActivation.Active).CooldownTicks != cooldown) return Fail("cooldown");
            var fixture = new CardBattleSetup(EntityId.New(), 6, 0, 0, [], UseLegacyAttack: false);
            var bench = fixture with { EntityId = EntityId.New(), BoardStart = 0, IsOnBench = true };
            var result = Run([queen, fixture, bench], [], cooldown + 30, reverse);
            var summons = result.Events.OfType<CardSummonedEvent>().ToArray();
            if (!summons.Select(e => e.BoardStart).SequenceEqual(new[] { 0, 1, 5, 7, 8, 9 })
                || summons.Any(e => e.Level != level || e.CardKey != new StringName("card.venom_bride") || e.Tick.Value != cooldown)) return Fail("summon positions");
            var poison = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.Poison && e.Amount > 0).ToArray();
            if (poison.Length != 6 || poison.Any(e => e.Tick.Value != cooldown + 30 || e.Amount != (level == 3 ? 10 : 16))
                || result.Events.OfType<CardDestroyedEvent>().Count() != 6
                || result.PermanentChanges.Any()) return Fail("summon aura consume");
            var repeated = Run([queen, fixture], [], cooldown * 2, reverse);
            if (repeated.Events.OfType<CardSummonedEvent>().Count() != 12) return Fail("refill");
            var sameSetup = Setup([queen, fixture, bench], [], cooldown + 30, reverse);
            if (!new CombatSimulator().Simulate(sameSetup).Events.SequenceEqual(new CombatSimulator().Simulate(sameSetup).Events)) return Fail("determinism");
            // 光环筛选：己方小型虫族受益；中型、非虫族、敌方及备战不受益。
            var insect = Card(new VenomBrideCardDefinition(), level, 0) with { Multicast = 2 };
            var medium = insect with { EntityId = EntityId.New(), BoardStart = 6, OccupiedSlots = 2, Multicast = 0 };
            var other = insect with { EntityId = EntityId.New(), BoardStart = 8, Tags = new TagSet(), Multicast = 0 };
            var enemy = insect with { EntityId = EntityId.New(), Multicast = 0 };
            var excludedBench = insect with { EntityId = EntityId.New(), IsOnBench = true };
            var filtered = Run([queen, insect, medium, other, excludedBench], [enemy], 45, reverse);
            if (filtered.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == insect.EntityId).Tick.Value != 30
                || filtered.Events.OfType<CardDestroyedEvent>().Single().CardId != insect.EntityId
                || filtered.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId != insect.EntityId).Any(e => e.Tick.Value != 40)) return Fail("filter and multicast");
            // 来源离场立即撤销：永久定义及自然消耗不被改写。
            var removedQueen = queen with { Abilities = queen.Abilities!.Concat(new[] { new AbilityDefinition("verification.queen.destroy", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]) }).ToArray() };
            var revoked = Run([removedQueen, insect with { Multicast = 0 }], [], 45, reverse);
            if (revoked.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == insect.EntityId).Tick.Value != 40
                || revoked.States[^1].Cards.Single(c => c.Id == insect.EntityId).Destroyed) return Fail("revoke");
            var benched = Run([queen with { IsOnBench = true }, insect with { Multicast = 0 }], [], cooldown, reverse);
            if (benched.Events.OfType<CardSummonedEvent>().Any() || benched.Events.OfType<CardDestroyedEvent>().Any()) return Fail("bench source");
            var secondQueen = queen with { EntityId = EntityId.New(), BoardStart = 5 };
            var stacked = Run([queen, secondQueen, insect with { Multicast = 0 }], [], 25, reverse);
            if (stacked.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == insect.EntityId).Tick.Value != 20
                || stacked.Events.OfType<CardDestroyedEvent>().Count() != 1) return Fail("stacked aura");
            var remaining = Run([removedQueen, secondQueen, insect with { Multicast = 0 }], [], 35, reverse);
            if (remaining.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == insect.EntityId).Tick.Value != 30) return Fail("precise aura revocation");
            var full = Run([queen, fixture with { BoardStart = 0, OccupiedSlots = 2 },
                fixture with { EntityId = EntityId.New(), BoardStart = 5, OccupiedSlots = 5 }], [], cooldown, reverse);
            if (full.Events.OfType<CardSummonedEvent>().Any()) return Fail("full battlefield");
        }
        return CardDescriptionChecks.AllLevels() && CombatTraceChecks.PreservesTraces();
    }
    private static bool Fail(string reason) { GD.Print("蜘蛛女皇失败：" + reason); return false; }
    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var instance = new EntityFactory().CreateCard(definition, level);
        var identity = definition.Attributes.Identity;
        return new CardBattleSetup(instance.Id, start, 0, instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks),
            instance.Abilities, UseLegacyAttack: false, Tags: instance.Tags, OccupiedSlots: identity.OccupiedSlots, ElementKeys: identity.ElementKeys)
            { Identity = identity, Level = level, CombatValues = instance.Attributes.BaseCombat.SnapshotFinalValues() };
    }
    private static BattleSetup Setup(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, bool reverse)
    {
        var ally = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), cards) { BattlefieldCapacity = 10 };
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), enemies) { BattlefieldCapacity = 10 };
        return new BattleSetup(reverse ? enemy : ally, reverse ? ally : enemy, 42, new BattleTick(ticks));
    }
    private static BattleResult Run(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, bool reverse) => new CombatSimulator().Simulate(Setup(cards, enemies, ticks, reverse));
    internal static async Task Capture(Control owner)
    {
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new SpiderQueenCardDefinition()));
        using var art = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage();
        if (art.GetWidth() * 2 != art.GetHeight() * 3 || !art.HasMipmaps()) throw new InvalidOperationException("蜘蛛女皇原画比例或mipmap失败。");
        GD.Print($"蜘蛛女皇原画 {art.GetWidth()}×{art.GetHeight()} mipmap={art.GetMipmapCount()}");
        var face = new CardItemView { Position = new Vector2(30, 40), Size = new Vector2(300, 200) }; owner.AddChild(face); face.Render(snapshot, new CardDisplayAdapter());
        var details = new CardDetailsView { Theme = MatchTheme.Create() }; owner.AddChild(details); details.SetEntries(CardCatalogQuery.Capture(DefinitionRegistry.Scan(typeof(SpiderQueenChecks).Assembly))); details.ShowCard(snapshot, new Vector2(350, 40), owner.GetViewportRect().Size);
        for (var i = 0; i < 20; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(); using var image = owner.GetViewport().GetTexture().GetImage(); image.SavePng("res://output/spider-queen/card-face.png");
        details.QueueFree(); face.QueueFree();
    }
}
