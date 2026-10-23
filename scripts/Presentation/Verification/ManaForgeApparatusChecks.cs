using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Gems;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

internal static class ManaForgeApparatusChecks
{
    internal static bool LevelsAndUpgrades()
    {
        var definition = new ManaForgeApparatusCardDefinition();
        if (definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.Attributes.Identity.Size != CardSize.Medium
            || definition.Attributes.Identity.FactionKey != new StringName("harla") || !definition.Tags.Contains(GameTags.Mechanical)
            || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })) return Fail("identity");
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var source = SetupCard(definition, level, 0);
            var target = SetupCard(new SteelWrenchCardDefinition(), 1, 2);
            var bench = target with { EntityId = EntityId.New(), BoardStart = 0, IsOnBench = true };
            var full = target with { EntityId = EntityId.New(), BoardStart = 4, Level = 4 };
            var enemy = target with { EntityId = EntityId.New(), BoardStart = 0 };
            var cooldown = level == 3 ? 50 : 40;
            var multiplier = level == 3 ? .98m : .96m;
            var setup = Setup([source, target, bench, full], [enemy], cooldown, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var upgrade = result.Events.OfType<CardUpgradedEvent>().Single();
            var frame = result.States[^1].Cards.Single(card => card.Id == target.EntityId);
            if (upgrade.CardId != target.EntityId || upgrade.PreviousLevel != 1 || upgrade.Level != 2 || upgrade.Tick.Value != cooldown
                || frame.Level != 2 || frame.Values[GameAttributeKeys.AttackDamage] != 20
                || frame.CooldownDurationUnits.Single() != 100m * multiplier
                || frame.UpgradedAbilities.Count == 0
                || result.Events.OfType<ManaChangedEvent>().Where(e => e.Amount < 0).Sum(e => e.Amount) != -50
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return Fail("battle upgrade and reward");
            if (level == 4 && frame.CooldownUnits.Single() != 16m) return Fail("remaining cooldown progress");
            var other = target with { EntityId = EntityId.New(), BoardStart = 5 };
            var chosen = new System.Collections.Generic.HashSet<EntityId>();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var randomResult = new CombatSimulator().Simulate(Setup([source, target, other], [], cooldown, reverse, seed: seed));
                chosen.Add(randomResult.Events.OfType<CardUpgradedEvent>().Single().CardId);
            }
            if (chosen.Count != 2) return Fail("random selection");
            var poor = new CombatSimulator().Simulate(Setup([source, target], [], cooldown + 5, reverse, mana: 49));
            if (poor.Events.OfType<CardUpgradedEvent>().Any() || poor.Events.OfType<ManaChangedEvent>().Any(e => e.Amount < 0)) return Fail("mana gate");
            var empty = new CombatSimulator().Simulate(Setup([source, full, bench], [], cooldown, reverse));
            if (empty.Events.OfType<CardUpgradedEvent>().Any()) return Fail("empty candidate");
            var benched = new CombatSimulator().Simulate(Setup([source with { IsOnBench = true }, target], [], cooldown + 5, reverse));
            if (benched.Events.OfType<CardUpgradedEvent>().Any()) return Fail("bench source");
            var death = target with { EntityId = EntityId.New(), BoardStart = 3, Abilities = [new AbilityDefinition("verification.forge.destroy", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] };
            for (ulong seed = 1; seed <= 4; seed++)
            {
                var trial = new CombatSimulator().Simulate(Setup([source, target, death], [], cooldown, reverse, seed: seed));
                if (trial.Events.OfType<CardUpgradedEvent>().Single().CardId != target.EntityId) return Fail("destroyed filter");
            }
            var additional = source with { EntityId = EntityId.New(), BoardStart = 6, UpgradeLevels = new System.Collections.Generic.Dictionary<int, BattleCardLevel>(), Abilities = source.Abilities!.Where(a => a.Activation != AbilityActivation.Active).ToArray() };
            var stacked = new CombatSimulator().Simulate(Setup([source, additional, target], [], cooldown, reverse));
            if (stacked.States[^1].Cards.Single(c => c.Id == target.EntityId).CooldownDurationUnits.Single() != 100m * multiplier * multiplier) return Fail("stacked rewards");
        }
        return PermanentAndFrozen() && CombatTraceChecks.PreservesTraces() && CardDescriptionChecks.AllLevels();
    }
    private static bool PermanentAndFrozen()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        foreach (var level in new[] { 3, 4 })
        {
            var session = new CreateMatchService(factory).Create(42, 100, new HarlaHeroDefinition());
            var opponent = new CreateMatchService(factory).Create(42, 100, new HarlaHeroDefinition());
            var forge = factory.CreateCard(new ManaForgeApparatusCardDefinition(), level);
            var targetDefinition = new SteelWrenchCardDefinition(); var target = factory.CreateCard(targetDefinition);
            session.Player.Inventory.Add(forge); session.Player.Inventory.Add(target);
            board.PlaceCard(session, forge.Id, BoardZone.Battlefield, 0); board.PlaceCard(session, target.Id, BoardZone.Battlefield, 2);
            target.Attributes.BaseCombat.ApplyModifier(new StatModifier(ModifierId.New(), forge.Id, GameAttributeKeys.AttackDamage, 7));
            if (new SocketGemService().Execute(session, new SocketGemCommand(session.Id, target.Id, 0, new EmeraldGemDefinition())).IsFailure)
                return Fail("socket setup");
            var before = MatchSnapshot.From(session); var opponentBefore = MatchSnapshot.From(opponent);
            var frozen = new BattleSetupFactory().Create(session, opponent, 42, new BattleTick(100));
            var percent = level == 3 ? 2 : 4; var factor = (100 - percent) / 100m;
            var merged = economy.AcquireCard(session, targetDefinition, 1, CardAcquisitionSource.Reward);
            if (merged.IsFailure || target.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2 || target.CooldownMultiplier != factor) return Fail("permanent merge");
            economy.AcquireCard(session, targetDefinition, 1, CardAcquisitionSource.Reward);
            var chain = economy.AcquireCard(session, targetDefinition, 1, CardAcquisitionSource.Reward);
            if (chain.IsFailure || chain.Value!.Card.Id != target.Id || target.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 3
                || target.CooldownMultiplier != factor * factor) return Fail("chained merge");
            CardLevelUpgradeService.Upgrade(session, target, targetDefinition, 4, factory);
            if (target.CooldownMultiplier != factor * factor * factor || target.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 4
                || frozen.Player.Cards.Single(card => card.EntityId == target.Id).Level != 1
                || frozen.Player.Cards.Single(card => card.EntityId == target.Id).CooldownMultiplier != 1m) return Fail("permanent levels and frozen input");
            var result = new CombatSimulator().Simulate(frozen);
            if (!result.Events.OfType<CardUpgradedEvent>().Any() || target.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 4
                || target.CooldownMultiplier != factor * factor * factor) return Fail("runtime isolation");
            var playback = new BattlePlaybackPresenter(new BattleResolution(before, opponentBefore, result, before));
            playback.Skip();
            var projected = playback.Project(SideId.Player).Cards.Single(card => card.Id == target.Id);
            if (projected.Level != 3 || projected.CurrentValues[GameAttributeKeys.AttackDamage] != 47
                || projected.BaseValues[GameAttributeKeys.AttackDamage] != 40
                || projected.CooldownMultiplier != factor * factor
                || !projected.Abilities.Single(a => a.Activation == AbilityActivation.Active).Effects.Any(e => e is ApplyPrimaryEffectPercentStatusEffectDefinition)
                || !playback.Capture().Feedback.Any(text => text.Contains("升至"))) return Fail("upgraded playback and gem");
            board.PlaceCard(session, forge.Id, BoardZone.Bench, 0);
            var benchTarget = factory.CreateCard(targetDefinition); session.Player.Inventory.Add(benchTarget);
            CardLevelUpgradeService.Upgrade(session, benchTarget, targetDefinition, 2, factory);
            if (benchTarget.CooldownMultiplier != 1m) return Fail("bench provider excluded");
            if (level == 3)
            {
                board.PlaceCard(session, forge.Id, BoardZone.Battlefield, 0);
                var selfMerge = economy.AcquireCard(session, new ManaForgeApparatusCardDefinition(), 3, CardAcquisitionSource.Reward);
                if (selfMerge.IsFailure || forge.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 4 || forge.CooldownMultiplier != .96m)
                    return Fail("provider self upgrade");
            }
        }
        return true;
    }
    private static bool Fail(string reason) { GD.Print("魔源锻造仪失败：" + reason); return false; }
    private static CardBattleSetup SetupCard(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level); var identity = card.Attributes.Identity;
        return new CardBattleSetup(card.Id, start, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), CardAbilityComposer.Compose(card), UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: identity.OccupiedSlots, ElementKeys: identity.ElementKeys)
            { Identity = identity, Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues(), UpgradeLevels = BattleCardLevels.Capture(definition, card) };
    }
    private static BattleSetup Setup(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, bool reverse, int mana = 100, ulong seed = 42)
    {
        var ally = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, InitialMana: mana, ManaRegen: 0), cards);
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, InitialMana: mana, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? enemy : ally, reverse ? ally : enemy, seed, new BattleTick(ticks));
    }
    internal static async Task Capture(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(ManaForgeApparatusChecks).Assembly);
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new ManaForgeApparatusCardDefinition()));
        using var art = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage();
        if (art.GetWidth() != art.GetHeight() || !art.HasMipmaps()) throw new InvalidOperationException("魔源锻造仪原画比例或mipmap失败。");
        GD.Print($"魔源锻造仪原画 {art.GetWidth()}×{art.GetHeight()} mipmap={art.GetMipmapCount()}");
        var face = new CardItemView { Position = new Vector2(30, 40), Size = new Vector2(200, 200) }; owner.AddChild(face); face.Render(snapshot, new CardDisplayAdapter());
        var small = new CardItemView { Position = new Vector2(60, 280), Size = new Vector2(100, 100) }; owner.AddChild(small); small.Render(snapshot, new CardDisplayAdapter());
        var details = new CardDetailsView { Theme = MatchTheme.Create() }; owner.AddChild(details); details.SetEntries(CardCatalogQuery.Capture(registry)); details.ShowCard(snapshot, new Vector2(270, 40), owner.GetViewportRect().Size);
        for (var i = 0; i < 20; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(); using var image = owner.GetViewport().GetTexture().GetImage(); image.SavePng("res://output/mana-forge-apparatus/card-face.png");
        details.QueueFree(); face.QueueFree(); small.QueueFree();
    }
}
