using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Encounters;
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

// 夜盗行衣疾速、轮初边界与跨英雄同级奖励验证（表现层）。
internal static class NightThiefGarbChecks
{
    internal static bool LevelsAndHaste()
    {
        var definition = new NightThiefGarbCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.DisplayName != "夜盗行衣" || identity.FactionKey != new StringName("valos")
            || identity.Size != CardSize.Medium || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.InitialLevel != 1 || definition.SupportsLevel(5) || definition.Tags.Count != 2
            || !definition.Tags.Contains(GameTags.Equipment) || identity.DescriptionEntries[1].KeywordKey != CardKeywords.RoundStart
            || CardKeywords.DisplayName(CardKeywords.RoundStart) != "轮初"
            || texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        for (var level = 1; level <= 4; level++)
        {
            var card = new EntityFactory().CreateCard(definition, level);
            if (card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 40
                || card.Abilities.Count != 2 || card.Abilities[0].ManaCost != 0) return false;
            var source = new CardBattleSetup(card.Id, 0, 0, 40, card.Abilities, UseLegacyAttack: false, OccupiedSlots: 2)
                { Level = level };
            foreach (var side in new[] { SideId.Player, SideId.Opponent })
            {
                var ally = new CardBattleSetup(EntityId.New(), 2, 0, 0, [], UseLegacyAttack: false);
                var bench = ally with { EntityId = EntityId.New(), IsOnBench = true };
                var enemy = ally with { EntityId = EntityId.New() };
                var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [source, ally, bench]);
                var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [enemy]);
                var selectedSelf = false; var selectedAlly = false;
                for (ulong seed = 1; seed <= 12; seed++)
                {
                    var setup = new BattleSetup(side == SideId.Player ? own : other, side == SideId.Player ? other : own,
                        seed, new BattleTick(40), new BattleTick(41));
                    var result = new CombatSimulator().Simulate(setup);
                    var applied = result.Events.OfType<StatusChangedEvent>().Single();
                    if (applied.Tick.Value != 40 || applied.Status != BattleStatus.HasteDuration || applied.Amount != level * 10
                        || result.Events.OfType<AbilityActivatedEvent>().Count() != 1
                        || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
                    var states = result.States[^1].Cards;
                    if (states.Single(item => item.Id == bench.EntityId).Haste != 0
                        || states.Single(item => item.Id == enemy.EntityId).Haste != 0) return false;
                    selectedSelf |= states.Single(item => item.Id == source.EntityId).Haste > 0;
                    selectedAlly |= states.Single(item => item.Id == ally.EntityId).Haste > 0;
                }
                if (!selectedSelf || !selectedAlly) return false;
            }
            var display = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.Details(display).Contains("轮初")
                || !CardDisplayAdapter.Details(display).Contains("当前英雄以外")) return false;
        }
        return DefinitionRegistry.Scan(typeof(NightThiefGarbChecks).Assembly).Cards.ContainsKey(identity.Key);
    }

    internal static bool RoundBoundariesAndPool()
    {
        var catalog = DefinitionRegistry.Scan(typeof(NightThiefGarbChecks).Assembly);
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver(), catalog.Sets);
        foreach (var hero in new HeroDefinition[] { new ValosHeroDefinition(), new RobinHeroDefinition() })
        for (var level = 1; level <= 4; level++)
        {
            var session = new CreateMatchService(factory).Create(42, 0, hero);
            var source = Add(session, board, new NightThiefGarbCardDefinition(), level, BoardZone.Battlefield, 0);
            Add(session, board, new NightThiefGarbCardDefinition(), 4, BoardZone.Bench, 0);
            var rounds = new RoundIncomeService(catalog.Sets, catalog);
            var random = new SeededRandom(session.Random.State);
            var pool = catalog.Cards.Values.Where(card => card.Attributes.Identity.Size == CardSize.Small
                && card.Attributes.Identity.FactionKey != hero.Attributes.Identity.FactionKey
                && card.SupportsLevel(level)
                && (card.Attributes.Identity.FactionKey == GameFactions.Neutral
                    || catalog.Heroes.Values.Any(owner => owner.Attributes.Identity.FactionKey == card.Attributes.Identity.FactionKey)))
                .OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray();
            var expected = pool[random.NextInt(0, pool.Length)];
            if (rounds.SettleCurrentRound(session).Value != 0 || session.Player.Inventory.Cards.Count != 2
                || session.Random.State == random.State) return false;
            session.Progress.Round = 2;
            if (rounds.SettleCurrentRound(session).IsFailure) return false;
            var reward = session.Player.Inventory.Cards.Single(card => card.Attributes.Identity.Key == expected.Attributes.Identity.Key);
            if (reward.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != level
                || session.Random.State != random.State || session.Player.Inventory.Cards.Count != 3
                || !session.Board.Contains(reward.Id)) return false;
            rounds.SettleCurrentRound(session);
            if (session.Player.Inventory.Cards.Count != 3 || session.Random.State != random.State) return false;
            board.PlaceCard(session, source.Id, BoardZone.Bench, 2);
            session.Progress.Round = 3; rounds.SettleCurrentRound(session);
            if (session.Random.State != random.State || session.Player.Inventory.Cards.Count != 3) return false;
        }
        var scheduled = new CreateMatchService(factory).Create(42, 0, new ValosHeroDefinition());
        Add(scheduled, board, new NightThiefGarbCardDefinition(), 3, BoardZone.Battlefield, 0);
        scheduled.Progress.Round = 2;
        var scheduler = new EncounterScheduler(catalog, allowIncompleteMonsterChoices: true);
        if (scheduler.Generate(scheduled).IsFailure || scheduled.Player.Inventory.Cards.Count != 2) return false;
        return scheduler.Generate(scheduled).IsSuccess && scheduled.Player.Inventory.Cards.Count == 2;
    }

    internal static bool MergeFullAndEmpty()
    {
        var catalog = DefinitionRegistry.Create([new RobinHeroDefinition(), new ValosHeroDefinition(),
            new NightThiefGarbCardDefinition(), new AssassinDaggerCardDefinition(),
            new LockpickCardDefinition()]);
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver(), catalog.Sets);
        // 1级池排除初始2级开锁器和中型行衣，只剩刺客匕首。
        var merge = new CreateMatchService(factory).Create(42, 0, new RobinHeroDefinition());
        Add(merge, board, new NightThiefGarbCardDefinition(), 1, BoardZone.Battlefield, 0);
        var dagger = Add(merge, board, new AssassinDaggerCardDefinition(), 1, BoardZone.Battlefield, 2);
        merge.Progress.Round = 2; new RoundIncomeService(catalog.Sets, catalog).SettleCurrentRound(merge);
        if (merge.Player.Inventory.Cards.Count != 2 || merge.Player.Inventory.Find(dagger.Id) is null
            || dagger.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2) return false;
        var full = new CreateMatchService(factory).Create(42, 0, new RobinHeroDefinition());
        Add(full, board, new NightThiefGarbCardDefinition(), 1, BoardZone.Battlefield, 0);
        for (var index = 2; index < 10; index++) Add(full, board, new BaaSheepCardDefinition(), 4, BoardZone.Battlefield, index);
        for (var index = 0; index < 10; index++) Add(full, board, new BaaSheepCardDefinition(), 4, BoardZone.Bench, index);
        full.Progress.Round = 2; var rounds = new RoundIncomeService(catalog.Sets, catalog);
        rounds.SettleCurrentRound(full); var state = full.Random.State; rounds.SettleCurrentRound(full);
        if (full.PendingMonsterRewards.Count != 1 || full.Random.State != state
            || full.PendingMonsterRewards.Single().Key != new StringName("card.assassin_dagger")
            || full.PendingMonsterRewards.Single().Level != 1 || full.Player.Inventory.Cards.Count != 19) return false;
        var empty = new CreateMatchService(factory).Create(42, 0, new ValosHeroDefinition());
        Add(empty, board, new NightThiefGarbCardDefinition(), 1, BoardZone.Battlefield, 0);
        empty.Progress.Round = 2; state = empty.Random.State;
        new RoundIncomeService(catalog.Sets, catalog).SettleCurrentRound(empty);
        if (empty.Player.Inventory.Cards.Count != 1 || empty.PendingMonsterRewards.Count != 0 || empty.Random.State != state) return false;
        var neutralCatalog = DefinitionRegistry.Create([new ValosHeroDefinition(), new NightThiefGarbCardDefinition(), new BaaSheepCardDefinition()]);
        var neutral = new CreateMatchService(factory).Create(42, 0, new ValosHeroDefinition());
        Add(neutral, board, new NightThiefGarbCardDefinition(), 2, BoardZone.Battlefield, 0);
        neutral.Progress.Round = 2; new RoundIncomeService(neutralCatalog.Sets, neutralCatalog).SettleCurrentRound(neutral);
        var sheep = neutral.Player.Inventory.Cards.Single(card => card.Attributes.Identity.FactionKey == GameFactions.Neutral);
        return neutral.Player.Inventory.Cards.Count == 2 && sheep.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 2;
    }

    internal static async Task Capture(Control owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(400, 400), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(400, 400) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(352, 352) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new NightThiefGarbCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/night-thief-garb/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Night thief garb capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardInstance Add(MatchSession session, BoardService board, CardDefinition definition,
        int level, BoardZone zone, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level); session.Player.Inventory.Add(card);
        if (board.PlaceCard(session, card.Id, zone, start).IsFailure) throw new InvalidOperationException("行衣验证放置失败。");
        return card;
    }
}
