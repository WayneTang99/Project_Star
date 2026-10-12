using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Random;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 伪装面具跨英雄光环、同级拾取及交易边界验证（表现层）。
internal static class DisguiseMaskChecks
{
    internal static bool LevelsAndAura()
    {
        var definition = new DisguiseMaskCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || identity.DisplayName != "伪装面具" || identity.FactionKey != new StringName("valos")
            || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Equipment)
            || texture is null || texture.GetHeight() != texture.GetWidth() * 2
            || identity.DescriptionEntries[1].KeywordKey != CardKeywords.Pickup
            || !DefinitionRegistry.Scan(typeof(DisguiseMaskChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, percent) in new[] { (2, 4), (3, 8), (4, 16) })
        foreach (var hero in new HeroDefinition[] { new ValosHeroDefinition(), new RobinHeroDefinition() })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var otherFaction = hero.Attributes.Identity.FactionKey == new StringName("valos") ? new StringName("robin") : new StringName("valos");
            var own = Receiver(0, hero.Attributes.Identity.FactionKey); var other = Receiver(1, otherFaction);
            var neutral = Receiver(2, GameFactions.Neutral); var bench = Receiver(0, GameFactions.Neutral) with { IsOnBench = true };
            var enemy = Receiver(0, GameFactions.Neutral);
            var setup = Battle([own, other, neutral, bench, Mask(level, 3), Mask(4, 1) with { IsOnBench = true }], [enemy], hero, 100, side);
            var result = new CombatSimulator().Simulate(setup);
            var states = result.States[0].Cards;
            if (states.Single(card => card.Id == own.EntityId).CooldownDurationUnits.Single() != 200
                || states.Single(card => card.Id == other.EntityId).CooldownDurationUnits.Single() != (100 - percent) * 2
                || states.Single(card => card.Id == neutral.EntityId).CooldownDurationUnits.Single() != (100 - percent) * 2
                || states.Single(card => card.Id == bench.EntityId).CooldownDurationUnits.Single() != 200
                || states.Single(card => card.Id == enemy.EntityId).CooldownDurationUnits.Single() != 200
                || result.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == neutral.EntityId).Tick.Value != 100 - percent
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var display = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var shortText = string.Join("\n", CardDisplayAdapter.Rules(display));
            if (!CardDisplayAdapter.Details(display).Contains("无阵营") || !shortText.Contains("拾取")
                || !shortText.Contains("小型或中型") || !shortText.Contains(percent + "%")) return false;
        }
        return Enum.GetValues<AbilityActivation>().Distinct().Count() == Enum.GetValues<AbilityActivation>().Length;
    }

    internal static bool AuraLifetimeAndFrozenIdentity()
    {
        var receiver = Receiver(0, GameFactions.Neutral) with { CooldownMultiplier = 0.8m };
        var first = Mask(2, 1);
        first = first with { Abilities = first.Abilities!.Append(new AbilityDefinition(new StringName("verification.mask.destroy"),
            AbilityActivation.Active, AbilityTarget.SelfCard, 0, 20, [new DestroyCardEffectDefinition(false)])).ToArray() };
        var result = new CombatSimulator().Simulate(Battle([receiver, first, Mask(4, 2)], [], new ValosHeroDefinition(), 100));
        if (result.States[0].Cards.Single(card => card.Id == receiver.EntityId).CooldownDurationUnits.Single() != 128m
            || result.States.Last(frame => frame.Tick.Value == 20).Cards.Single(card => card.Id == receiver.EntityId).CooldownDurationUnits.Single() != 134.4m
            || result.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == receiver.EntityId).Tick.Value != 68
            || receiver.CooldownMultiplier != 0.8m || result.PermanentChanges.Count != 0) return false;
        var capped = new CombatSimulator().Simulate(Battle(Enumerable.Range(1, 7).Select(start => Mask(4, start)).Prepend(receiver).ToArray(),
            [], new ValosHeroDefinition(), 3));
        if (capped.States[0].Cards.Single(card => card.Id == receiver.EntityId).CooldownDurationUnits.Single() != 0
            || capped.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == receiver.EntityId) != 3) return false;
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var session = new CreateMatchService(factory).Create(42, 0, new ValosHeroDefinition());
        var opponent = new CreateMatchService(factory).Create(2, 0, new RobinHeroDefinition());
        Add(session, board, new DisguiseMaskCardDefinition(), 4, 0);
        var dagger = Add(session, board, new AssassinDaggerCardDefinition(), 2, 1);
        var sheep = Add(session, board, new BaaSheepCardDefinition(), 1, 2);
        var frozen = new BattleSetupFactory().Create(session, opponent, 42, new BattleTick(50));
        session.Player.SelectHero(factory.CreateHero(new RobinHeroDefinition()));
        result = new CombatSimulator().Simulate(frozen);
        return result.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == dagger.Id).Tick.Value == 40
            && result.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == sheep.Id).Tick.Value == 42;
    }

    internal static bool PickupLevelsAndTransactions()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var definition = new DisguiseMaskCardDefinition();
        CardDefinition[] cards = [definition, new BaaSheepCardDefinition(), new BoarCardDefinition(), new AssassinDaggerCardDefinition(), new WarhammerFortressCardDefinition()];
        var economy = new CardEconomyService(factory, board, cards);
        var smallSeen = false; var mediumSeen = false;
        for (ulong seed = 1; seed <= 12; seed++)
        foreach (var level in new[] { 2, 3, 4 })
        {
            var session = new CreateMatchService(factory).Create(seed, 100, new ValosHeroDefinition());
            var random = new SeededRandom(session.Random.State);
            var candidates = cards.Where(card => card.Attributes.Identity.FactionKey == GameFactions.Neutral
                && card.Attributes.Identity.Size is CardSize.Small or CardSize.Medium)
                .OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray();
            var expected = candidates[random.NextInt(0, candidates.Length)];
            var offer = ShopOffer.Create(definition, level);
            var acquired = economy.BuyAndPlace(session, offer);
            if (acquired.IsFailure || session.Player.Inventory.Cards.Count != 2 || session.Random.State != random.State) return false;
            var reward = session.Player.Inventory.Cards.Single(card => card.Attributes.Identity.Key == expected.Attributes.Identity.Key);
            if (reward.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != level || !session.Board.Contains(reward.Id)
                || !session.Board.Contains(acquired.Value!.Id)) return false;
            smallSeen |= reward.Attributes.Identity.Size == CardSize.Small; mediumSeen |= reward.Attributes.Identity.Size == CardSize.Medium;
            if (economy.BuyAndPlace(session, offer).IsSuccess || session.Player.Inventory.Cards.Count != 2 || session.Random.State != random.State) return false;
        }
        var merge = new CreateMatchService(factory).Create(42, 0, new ValosHeroDefinition());
        var mask = economy.AcquireAndPlace(merge, definition, 2, CardAcquisitionSource.Reward).Value!.Card;
        var again = economy.AcquireAndPlace(merge, definition, 2, CardAcquisitionSource.Reward);
        if (again.IsFailure || again.Value!.Id != mask.Id || mask.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 3
            || !merge.Player.Inventory.Cards.Any(card => card.Attributes.Identity.FactionKey == GameFactions.Neutral
                && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3)) return false;
        var final = economy.AcquireAndPlace(merge, definition, 3, CardAcquisitionSource.Reward);
        return smallSeen && mediumSeen && final.IsSuccess && mask.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4
            && merge.Player.Inventory.Cards.Any(card => card.Attributes.Identity.FactionKey == GameFactions.Neutral
                && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4);
    }

    internal static bool PickupFullEmptyAndSharedRandom()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var definition = new DisguiseMaskCardDefinition();
        var economy = new CardEconomyService(factory, board, [definition, new BaaSheepCardDefinition()]);
        var full = new CreateMatchService(factory).Create(42, 100, new ValosHeroDefinition());
        for (var start = 0; start < 10; start++) Add(full, board, new BaaSheepCardDefinition(), 4, start);
        for (var start = 0; start < 9; start++)
        {
            var filler = factory.CreateCard(new BaaSheepCardDefinition(), 4); full.Player.Inventory.Add(filler);
            board.PlaceCard(full, filler.Id, BoardZone.Bench, start);
        }
        var offer = ShopOffer.Create(definition, 2);
        if (economy.BuyAndPlace(full, offer).IsFailure || full.PendingMonsterRewards.Count != 1
            || full.PendingMonsterRewards.Single().Level != 2 || full.Player.Inventory.Cards.Count != 20) return false;
        var state = full.Random.State; var failedOffer = ShopOffer.Create(definition, 4);
        if (economy.BuyAndPlace(full, failedOffer).IsSuccess || failedOffer.IsSold || full.Random.State != state
            || full.PendingMonsterRewards.Count != 1) return false;
        var empty = new CreateMatchService(factory).Create(42, 0, new ValosHeroDefinition());
        new CardEconomyService(factory, board, [definition]).AcquireAndPlace(empty, definition, 2, CardAcquisitionSource.Reward);
        if (empty.Player.Inventory.Cards.Count != 1 || empty.Random.State != 42) return false;
        var shared = new SeededRandom(99); shared.NextInt(0, 3);
        var expected = new SeededRandom(shared.State); expected.NextInt(0, 1);
        var session = new CreateMatchService(factory).Create(42, 0, new ValosHeroDefinition());
        economy.AcquireAndPlace(session, definition, 2, CardAcquisitionSource.Reward, shared);
        if (shared.State != expected.State || session.Random.State != expected.State || session.Player.Inventory.Cards.Count != 2) return false;
        // 跨英雄持有面具时，可随机获得面具自身；合并与连锁拾取到满盘后保留一张待领。
        var chain = new CreateMatchService(factory).Create(42, 0, new RobinHeroDefinition());
        var chainEconomy = new CardEconomyService(factory, board, [definition]);
        if (chainEconomy.AcquireAndPlace(chain, definition, 2, CardAcquisitionSource.Reward).IsFailure) return false;
        return chain.Player.Inventory.Cards.Count == 20 && chain.PendingMonsterRewards.Count == 1
            && chain.PendingMonsterRewards.Single().Level == 4
            && chain.Player.Inventory.Cards.All(card => card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4);
    }

    internal static async Task Capture(Control owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(280, 440), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(280, 440) });
            var face = new CardItemView { Position = new Vector2(40, 20), Size = new Vector2(200, 400) }; viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new DisguiseMaskCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/disguise-mask/card-face.png") != Error.Ok) throw new InvalidOperationException("面具卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardInstance Add(MatchSession session, BoardService board, CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level); session.Player.Inventory.Add(card);
        if (board.PlaceCard(session, card.Id, BoardZone.Battlefield, start).IsFailure) throw new InvalidOperationException("面具验证放置失败。");
        return card;
    }
    private static CardBattleSetup Mask(int level, int start) => new(EntityId.New(), start, 0, 0,
        new DisguiseMaskCardDefinition().GetLevel(level)!.Abilities, UseLegacyAttack: false)
        { Identity = new DisguiseMaskCardDefinition().Attributes.Identity, Level = level };
    private static CardBattleSetup Receiver(int start, StringName faction) => new(EntityId.New(), start, 1, 100,
        [new AbilityDefinition(new StringName("verification.mask.attack"), AbilityActivation.Active, AbilityTarget.EnemyHero,
            0, 100, [new DamageEffectDefinition(1)])], UseLegacyAttack: false)
        { Identity = new CardIdentityAttributes(new StringName("verification.mask.receiver"), "光环目标", faction, CardSize.Small, [GameElements.General]) };
    private static BattleSetup Battle(CardBattleSetup[] own, CardBattleSetup[] other, HeroDefinition hero, int ticks, SideId side = SideId.Player) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0) { Identity = hero.Attributes.Identity }, side == SideId.Player ? own : other),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0) { Identity = hero.Attributes.Identity }, side == SideId.Player ? other : own),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
