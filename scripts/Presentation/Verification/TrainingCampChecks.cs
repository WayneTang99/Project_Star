using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
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
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 训练营分级群体训练、标签拾取与交易边界验证（表现层）。
internal static class TrainingCampChecks
{
    internal static bool LevelsAndTraining()
    {
        var definition = new TrainingCampCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || identity.DisplayName != "训练营" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Medium || !definition.Tags.Contains(GameTags.Location)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || texture is null || texture.GetWidth() != texture.GetHeight()
            || !DefinitionRegistry.Scan(typeof(TrainingCampChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, amount) in new[] { (2, 5), (3, 10), (4, 20) })
        foreach (var reverse in new[] { false, true })
        {
            var camp = Camp(level); var first = Human(2, 50); var zero = Human(3, 0);
            var support = Human(4, 0) with { Abilities = [] };
            var bench = Human(0, 50) with { IsOnBench = true };
            var dead = Human(5, 50); dead = dead with { Abilities = dead.Abilities!.Append(DestroyAtStart()).ToArray() };
            var beast = Human(6, 50) with { Tags = new TagSet([GameTags.Beast]) };
            var enemy = Human(0, 50);
            var setup = Battle([camp, first, zero, support, bench, dead, beast], [enemy], 75, reverse);
            var result = new CombatSimulator().Simulate(setup);
            if (Attack(result, first) != 50 + amount * 2 || Attack(result, zero) != amount * 2
                || Attack(result, support) != amount * 2 || Attack(result, bench) != 50 || Attack(result, dead) != 50
                || Attack(result, beast) != 50 || Attack(result, enemy) != 50 || Attack(result, camp) != 0
                || result.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == first.EntityId).RawDamage != 50 + amount * 2
                || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == camp.EntityId) != 2
                || result.PermanentAttributeBonuses.Count != 0
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            foreach (var inactive in new[] { camp with { IsOnBench = true },
                camp with { Abilities = camp.Abilities!.Append(DestroyAtStart()).ToArray() } })
                if (Attack(new CombatSimulator().Simulate(Battle([inactive, first], [], 75, reverse)), first) != 50) return false;
            var loss = camp with { Abilities = camp.Abilities!.Append(new AbilityDefinition(
                new StringName("verification.camp.destroy_after_training"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 40, [new DestroyCardEffectDefinition(false)])).ToArray() };
            if (Attack(new CombatSimulator().Simulate(Battle([loss, first], [], 75, reverse)), first) != 50 + amount
                || Attack(new CombatSimulator().Simulate(Battle([camp with { Multicast = 1 }, first], [], 62, reverse)), first) != 50 + amount * 4
                || Attack(new CombatSimulator().Simulate(Battle([first], [], 1, reverse)), first) != 50) return false;
            if (new CombatSimulator().Simulate(Battle([camp], [], 60, reverse)).Events.OfType<CardAttributeChangedEvent>().Any()) return false;
            var display = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.Details(display).Contains("人类")
                || !CardDisplayAdapter.Details(display).Contains("同级小型")) return false;
        }
        return true;
    }

    internal static bool PickupAndBoundaries()
    {
        var definition = new TrainingCampCardDefinition();
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        CardDefinition[] cards = [definition, new ApprenticeSwordsmanCardDefinition(), new AssassinDaggerCardDefinition(),
            new ShortbladeScoutCardDefinition(), new NunCardDefinition(), new BonebreakerMageCardDefinition(),
            new MoonshadowCatCardDefinition(), new ShieldBearerCardDefinition(), new BaaSheepCardDefinition()];
        var economy = new CardEconomyService(factory, board, cards);
        foreach (var level in new[] { 2, 3, 4 })
        for (ulong seed = 1; seed <= 6; seed++)
        {
            var session = new CreateMatchService(factory).Create(seed, 100, new PaladinHeroDefinition());
            var random = new SeededRandom(session.Random.State);
            var candidates = cards.Where(card => card.Attributes.Identity.Size == CardSize.Small
                && card.Tags.Contains(GameTags.Human) && card.SupportsLevel(level))
                .OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray();
            var expected = candidates[random.NextInt(0, candidates.Length)];
            var offer = ShopOffer.Create(definition, level);
            if (economy.BuyAndPlace(session, offer).IsFailure || session.Player.Inventory.Cards.Count != 2
                || session.Random.State != random.State) return false;
            var reward = session.Player.Inventory.Cards.Single(card => card.Attributes.Identity.Key == expected.Attributes.Identity.Key);
            if (reward.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != level || !session.Board.Contains(reward.Id)) return false;
            if (economy.BuyAndPlace(session, offer).IsSuccess || session.Random.State != random.State
                || session.Player.Inventory.Cards.Count != 2) return false;
        }
        // 单候选保证同名奖励走合并，奖励按训练营合并后的等级结算。
        var only = new CardEconomyService(factory, board, [definition, new ApprenticeSwordsmanCardDefinition()]);
        var merge = new CreateMatchService(factory).Create(42, 0, new PaladinHeroDefinition());
        var camp = only.AcquireAndPlace(merge, definition, 2, CardAcquisitionSource.Reward).Value!.Card;
        var again = only.AcquireAndPlace(merge, definition, 2, CardAcquisitionSource.Reward);
        if (again.IsFailure || again.Value!.Id != camp.Id || camp.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 3
            || !merge.Player.Inventory.Cards.Any(card => card.Attributes.Identity.Key == new StringName("card.apprentice_swordsman")
                && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3)) return false;
        var empty = new CreateMatchService(factory).Create(42, 0, new PaladinHeroDefinition());
        new CardEconomyService(factory, board, [definition, new AssassinDaggerCardDefinition(), new ShieldBearerCardDefinition()])
            .AcquireAndPlace(empty, definition, 2, CardAcquisitionSource.Reward);
        if (empty.Random.State != 42 || empty.Player.Inventory.Cards.Count != 1) return false;
        var full = new CreateMatchService(factory).Create(42, 100, new PaladinHeroDefinition());
        for (var start = 0; start < 10; start++) AddSheep(full, board, BoardZone.Battlefield, start);
        for (var start = 0; start < 8; start++) AddSheep(full, board, BoardZone.Bench, start);
        if (only.BuyAndPlace(full, ShopOffer.Create(definition, 2)).IsFailure || full.PendingMonsterRewards.Count != 1
            || full.PendingMonsterRewards.Single().Level != 2
            || full.PendingMonsterRewards.Single().Key != new StringName("card.apprentice_swordsman")) return false;
        var state = full.Random.State; var wealth = full.Player.Wealth; var failed = ShopOffer.Create(definition, 4);
        if (only.BuyAndPlace(full, failed).IsSuccess || failed.IsSold || full.Random.State != state
            || full.Player.Wealth != wealth || full.PendingMonsterRewards.Count != 1) return false;
        var shared = new SeededRandom(99); shared.NextInt(0, 3);
        var expectedRandom = new SeededRandom(shared.State); expectedRandom.NextInt(0, 1);
        var sharedSession = new CreateMatchService(factory).Create(42, 0, new PaladinHeroDefinition());
        if (only.AcquireAndPlace(sharedSession, definition, 2, CardAcquisitionSource.Reward, shared).IsFailure
            || sharedSession.Random.State != expectedRandom.State || shared.State != expectedRandom.State) return false;
        return true;
    }

    internal static async Task Capture(Control owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(448, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(448, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(400, 400) }; viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new TrainingCampCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/training-camp/card-face.png") != Error.Ok)
                throw new InvalidOperationException("训练营卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static void AddSheep(MatchSession session, BoardService board, BoardZone zone, int start)
    {
        var card = new EntityFactory().CreateCard(new BaaSheepCardDefinition(), 4); session.Player.Inventory.Add(card);
        if (board.PlaceCard(session, card.Id, zone, start).IsFailure) throw new InvalidOperationException("训练营满盘夹具放置失败。");
    }
    private static CardBattleSetup Camp(int level)
    {
        var card = new EntityFactory().CreateCard(new TrainingCampCardDefinition(), level);
        return new(card.Id, 0, 0, 30, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2) { Level = level };
    }
    private static CardBattleSetup Human(int start, int attack) => new(EntityId.New(), start, attack, 75,
        [new AbilityDefinition(new StringName("verification.camp.attack"), AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 75,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])], UseLegacyAttack: false, Tags: new TagSet([GameTags.Human]));
    private static AbilityDefinition DestroyAtStart() => new(new StringName("verification.camp.destroy"), AbilityActivation.PassiveOnBattleStart,
        AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);
    private static int Attack(BattleResult result, CardBattleSetup card) => result.States.Last().Cards.Single(c => c.Id == card.EntityId).Values[GameAttributeKeys.AttackDamage];
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse = false) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? enemies : allies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? allies : enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
