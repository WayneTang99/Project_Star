using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Application.Mentors;
using Project_Star.Content.Encounters;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;
using Project_Star.Infrastructure.Random;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 暮歌丛林的固定选项、概率边界、奖励与怪物战流程验证（表现层）。
internal static class DuskSongJungleChecks
{
    internal static bool Definitions()
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var definition = (DuskSongJungleEncounterDefinition)registry.Encounters[new StringName("encounter.dusk_song_jungle")];
        if (definition.Attributes.Identity.DisplayName != "暮歌丛林" || definition.Level != 2
            || definition.MinimumRound != 1 || definition.MaximumRound != 99 || definition.BaseWeight != 1
            || definition.Options.Count != 2 || definition.OptionSlots.Count != 2
            || definition.Options[0].DisplayName != "采摘" || definition.Options[1].DisplayName != "砍伐") return false;
        var gather = (GrantRandomTaggedCardEncounterOptionEffectDefinition)definition.Options[0].Effects.Single();
        var fell = (WeightedEncounterOptionEffectDefinition)definition.Options[1].Effects.Single();
        if (gather.RequiredTag != GameTags.Plant || gather.Size != CardSize.Small || !gather.UseEncounterLevel
            || !fell.Outcomes.Select(outcome => outcome.Weight).SequenceEqual(new[] { 20, 30, 50 })
            || fell.Outcomes[0].Effect is not GrantRandomTaggedCardEncounterOptionEffectDefinition { Size: CardSize.Large, UseEncounterLevel: true }
            || fell.Outcomes[1].Effect is not GrantRandomTaggedCardEncounterOptionEffectDefinition { Size: CardSize.Medium, UseEncounterLevel: true }
            || fell.Outcomes[2].Effect is not EnterRandomMonsterEncounterOptionEffectDefinition) return false;
        var explanation = PlaytestText.FormatOption(definition.Options[1], 1, 3);
        if (!explanation.Contains("20%") || !explanation.Contains("30%") || !explanation.Contains("50%")
            || !explanation.Contains("3 级大型植物") || !explanation.Contains("3 级怪物")) return false;
        var texture = GD.Load<Texture2D>(definition.Attributes.Identity.Illustration.ToString());
        if (texture.GetWidth() != 1536 || texture.GetHeight() != 1024) return false;
        foreach (var weight in new[] { 0, -1, int.MaxValue })
        {
            try
            {
                DefinitionRegistry.Create([new EffectEncounter(new WeightedEncounterOptionEffectDefinition(
                    [new(new GainWealthEncounterOptionEffectDefinition(1), weight), new(new GainWealthEncounterOptionEffectDefinition(1), 1)]))]);
                return false;
            }
            catch (DefinitionValidationException) { }
        }
        return true;
    }

    internal static bool Outcomes()
    {
        var forest = new DuskSongJungleEncounterDefinition();
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var matches = new CreateMatchService(factory);
        CardDefinition[] plants = [new PlantCard(CardSize.Small), new PlantCard(CardSize.Medium), new PlantCard(CardSize.Large)];
        MonsterDefinition[] monsters = [new TestMonster("first", 2, 2, 2), new TestMonster("second", 2, 2, 2),
            new TestMonster("future", 2, 3, 4), new TestMonster("past", 2, 1, 1), new TestMonster("wrong_level", 3, 2, 2)];
        var resolver = new ResolveEncounterOptionService(factory, board, plants, monsters);
        var monsterRegistry = DefinitionRegistry.Create(monsters.Cast<object>());
        foreach (var round in new[] { 1, 2, 3, 4 })
        {
            var session = new MatchSession(1); session.Progress.Round = round; session.Progress.Turn = 4;
            var choices = new EncounterScheduler(monsterRegistry, allowIncompleteMonsterChoices: true).Generate(session);
            if (choices.IsFailure || choices.Value!.Any(choice =>
                monsterRegistry.Monsters[choice.Key].MinimumRound > round || monsterRegistry.Monsters[choice.Key].MaximumRound < round)) return false;
        }
        foreach (var roll in new[] { 0, 19, 20, 49, 50, 99 })
        {
            var seed = SeedForRoll(roll);
            var first = Resolve(seed); var second = Resolve(seed);
            if (!first.Options.IsResolved || first.Result.IsFailure || second.Result.IsFailure
                || first.Session.Random.State != second.Session.Random.State || first.Session.Progress.Turn != 1) return false;
            var result = first.Result.Value!;
            if (roll < 50)
            {
                if (result.GrantedCard is not { } card || card.Attributes.Identity.Size != (roll < 20 ? CardSize.Large : CardSize.Medium)
                    || !card.Tags.Contains(GameTags.Plant) || card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2
                    || first.Session.Board.Battlefield.Count != 1 || result.MonsterEncounter is not null
                    || card.Attributes.Identity.Key != second.Result.Value!.GrantedCard!.Attributes.Identity.Key) return false;
            }
            else if (result.GrantedCard is not null || result.MonsterEncounter is not { Level: 2 } monster
                || monster.Key != monsters[0].Attributes.Identity.Key && monster.Key != monsters[1].Attributes.Identity.Key
                || result.MonsterEncounter != second.Result.Value!.MonsterEncounter || first.Session.Player.Inventory.Cards.Count != 0) return false;
            var state = first.Session.Random.State;
            if (resolver.Resolve(first.Session, first.Options, forest.Options[1].Key).IsSuccess || first.Session.Random.State != state) return false;
        }
        foreach (var encounterLevel in new[] { 2, 3, 4 })
        {
            var session = matches.Create(1, 0, new PaladinHeroDefinition());
            var options = resolver.CreateOptionSet(session, forest, encounterLevel);
            var result = resolver.Resolve(session, options, forest.Options[0].Key);
            if (result.Value?.GrantedCard?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != encounterLevel) return false;
        }
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var formalResolver = new ResolveEncounterOptionService(factory, board, registry.Cards.Values, registry.Monsters.Values);
        var gatherSession = matches.Create(1, 0, new PaladinHeroDefinition());
        var gatherResult = formalResolver.Resolve(gatherSession, formalResolver.CreateOptionSet(gatherSession, forest, 4), forest.Options[0].Key);
        if (gatherResult.Value?.GrantedCard is not { } plant || !plant.Tags.Contains(GameTags.Plant)
            || plant.Attributes.Identity.Size != CardSize.Small
            || plant.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 4) return false;
        var mergeSession = matches.Create(2, 0, new PaladinHeroDefinition());
        var existing = new CardEconomyService(factory, board).AcquireCard(mergeSession, plants[0], 2, CardAcquisitionSource.Reward).Value!.Card;
        board.PlaceCard(mergeSession, existing.Id, BoardZone.Battlefield, 0);
        var merged = resolver.Resolve(mergeSession, resolver.CreateOptionSet(mergeSession, forest), forest.Options[0].Key);
        if (merged.Value?.GrantedCard?.Id != existing.Id || existing.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 3
            || mergeSession.Player.Inventory.Cards.Count != 1) return false;
        var full = matches.Create(3, 0, new PaladinHeroDefinition());
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        for (var slot = 0; slot < full.Board.Battlefield.Capacity; slot++)
        {
            var card = factory.CreateCard(plants[0], 4); full.Player.Inventory.Add(card); board.PlaceCard(full, card.Id, zone, slot);
        }
        var fullResult = resolver.Resolve(full, resolver.CreateOptionSet(full, forest), forest.Options[0].Key);
        if (!fullResult.IsSuccess || fullResult.Value!.GrantedCard is not null || !fullResult.Value.CardRewardSkipped) return false;
        var empty = new ResolveEncounterOptionService(factory, board, plants);
        var noMonster = matches.Create(SeedForRoll(50), 0, new PaladinHeroDefinition());
        var noMonsterOptions = empty.CreateOptionSet(noMonster, forest); noMonster.Random.State = SeedForRoll(50);
        var noMonsterResult = empty.Resolve(noMonster, noMonsterOptions, forest.Options[1].Key);
        if (!noMonsterOptions.IsResolved || !noMonsterResult.Value!.MonsterEncounterSkipped
            || !PlaytestText.FormatEventResult(noMonsterResult.Value).Contains("没有符合当前轮次")) return false;
        var emptyCards = new ResolveEncounterOptionService(factory, board, Array.Empty<CardDefinition>());
        var failed = matches.Create(4, 0, new PaladinHeroDefinition());
        var failedOptions = emptyCards.CreateOptionSet(failed, forest); failed.Random.State = SeedForRoll(0);
        var originalState = failed.Random.State;
        var failure = emptyCards.Resolve(failed, failedOptions, forest.Options[1].Key);
        return failure.IsFailure && !failedOptions.IsResolved && failed.Random.State == originalState;

        (MatchSession Session, EncounterOptionSet Options, Project_Star.Application.Common.Result<EncounterOptionResult> Result) Resolve(ulong seed)
        {
            var session = matches.Create(seed, 0, new PaladinHeroDefinition()); session.Progress.Round = 2;
            var options = resolver.CreateOptionSet(session, forest); session.Random.State = seed;
            if (options.Options.Count != 2 || options.Options[1].DisplayName != "砍伐") throw new InvalidOperationException("固定展示槽被随机结果替代。");
            return (session, options, resolver.Resolve(session, options, forest.Options[1].Key));
        }
    }

    internal static bool Flow()
    {
        var presenter = CreatePresenter(); presenter.Reset(); presenter.SelectHero(new StringName("hero.paladin"));
        var choice = presenter.View.Choices.Single(item => item.Key == new StringName("encounter.dusk_song_jungle"));
        if (choice.Level != 2) return false;
        presenter.ChooseEncounter(choice.Key);
        if (presenter.View.Page != MatchPage.Event || presenter.View.EventOptions.Count != 2) return false;
        var option = presenter.View.EventOptions[1]; var revision = presenter.View.EventRevision;
        presenter.ResolveEventOption(option.Key, revision - 1);
        if (presenter.View.Page != MatchPage.Event) return false;
        presenter.ResolveEventOption(option.Key, revision);
        if (presenter.View.Page != MatchPage.Preparation || presenter.View.Enemy?.Hero?.Level != 2
            || presenter.View.Player?.Turn != 2 || presenter.View.DisplayTurn != 1 || presenter.View.EncounterLevel != 2) return false;
        presenter.ResolveEventOption(option.Key, revision);
        if (presenter.View.Page != MatchPage.Preparation) return false;
        presenter.StartBattle();
        if (presenter.View.Page != MatchPage.BattlePlayback) return false;
        presenter.SkipPlayback(); presenter.ContinueMatch();
        return presenter.View.Page == MatchPage.EncounterChoice && presenter.View.Player?.Turn == 2
            && presenter.View.Enemy is null && presenter.View.DisplayTurn == 2;
    }

    internal static MatchPresenter CreatePresenter()
    {
        var registry = DefinitionRegistry.Create([new PaladinHeroDefinition(), new DuskSongJungleEncounterDefinition(),
            new SmallShopEncounterDefinition(), new PlantCard(CardSize.Small), new PlantCard(CardSize.Medium),
            new PlantCard(CardSize.Large), new TestMonster("flow", 2, 1, 1)]);
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board, registry.Cards.Values);
        var game = new GameCoordinator(new CreateMatchService(factory), new EncounterScheduler(registry, allowIncompleteMonsterChoices: true),
            new StartBattleService(new BattleSetupFactory(registry.Sets), new CombatSimulator()), new MatchResultService(board));
        return new MatchPresenter(registry, board, economy, new ShopCardPoolService(),
            new ResolveEncounterOptionService(factory, board, registry.Cards.Values, registry.Monsters.Values),
            new MentorService(registry, new SkillAcquisitionService(factory)),
            new MonsterRewardClaimService(registry, economy, new SkillAcquisitionService(factory), board), game,
            new LocalTestOpponentProvider(registry));
    }

    // 正式遭遇定义配合内部植物/怪物夹具的两档原生窗口验证。
    internal static async Task Capture(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<Control>();
        var shell = scene.GetNode<MatchShell>("MatchShell"); scene.RemoveChild(shell); scene.Free();
        owner.AddChild(shell); shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var presenter = CreatePresenter(); presenter.ViewChanged += shell.Render;
        shell.ChoiceSelected += (_, key, revision) => presenter.ResolveEventOption(key, revision);
        try
        {
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                owner.GetWindow().Size = size; owner.GetTree().Root.ContentScaleSize = size;
                presenter.Reset(); presenter.SelectHero(new StringName("hero.paladin"));
                presenter.ChooseEncounter(new StringName("encounter.dusk_song_jungle"));
                await Frame(); await Frame();
                var eventView = shell.GetNode<Control>("ContextRow/ContextHost/EventView");
                var actions = eventView.GetNode<VBoxContainer>("ActionScroll/Actions");
                var fell = actions.GetChild<Button>(1);
                await Save("event");
                if (actions.GetChildCount() != 2 || !fell.Text.Contains("50%")
                    || fell.GetGlobalRect().End.X > size.X || fell.GetGlobalRect().End.Y > eventView.GetGlobalRect().End.Y + 1)
                    throw new InvalidOperationException("丛林选项、概率说明或窗口边界异常。");
                eventView.GetNode<ScrollContainer>("ActionScroll").EnsureControlVisible(fell); await Frame();
                Click(fell); await Frame();
                if (presenter.View.Page != MatchPage.Preparation || presenter.View.Player?.Turn != 2)
                    throw new InvalidOperationException("原生砍伐按钮没有进入同回合怪物战。");
                await Save("monster-fixture");
                presenter.Reset(); presenter.SelectHero(new StringName("hero.paladin"));
                presenter.ChooseEncounter(new StringName("encounter.dusk_song_jungle")); await Frame(); await Frame();
                eventView.GetNode<ScrollContainer>("ActionScroll").EnsureControlVisible(actions.GetChild<Button>(0)); await Frame();
                Click(actions.GetChild<Button>(0)); await Frame();
                if (presenter.View.Player?.Cards.Count != 1 || presenter.View.Player.Cards[0].Level != 2
                    || !presenter.View.Continue.Enabled) throw new InvalidOperationException("原生采摘按钮没有完成奖励。");
                await Save("gather-fixture");
            }
            GD.Print("暮歌丛林两档窗口、概率说明、原生采摘奖励和砍伐怪物转场通过（奖励与怪物使用内部夹具）。");
        }
        finally { presenter.ViewChanged -= shell.Render; owner.RemoveChild(shell); shell.Free(); }

        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        void Click(Button button)
        {
            var point = button.GetGlobalRect().GetCenter();
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        }
        async Task Save(string name)
        {
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            const string directory = "res://output/dusk-song-jungle-2026-10-09";
            using var image = owner.GetViewport().GetTexture().GetImage(); image.Convert(Image.Format.Rgba8);
            if (image.SavePng($"{directory}/{name}-{image.GetWidth()}x{image.GetHeight()}.png") != Error.Ok)
                throw new InvalidOperationException("暮歌丛林截图保存失败。");
        }
    }

    private static ulong SeedForRoll(int roll)
    {
        for (ulong seed = 0; seed < 10000; seed++) if (new SeededRandom(seed).NextInt(0, 100) == roll) return seed;
        throw new InvalidOperationException("未找到概率边界验证seed。");
    }

    // 可复用效果的内部验证遭遇，不注册到正式内容（表现层夹具）。
    private sealed class EffectEncounter : ChoiceEncounterDefinition
    {
        internal EffectEncounter(EncounterOptionEffectDefinition effect) : base(new EntityAttributes<EncounterIdentityAttributes>(
            new EncounterIdentityAttributes(new StringName("encounter.verification.effect"), "效果验证")), 1, 99,
            [new EncounterOptionDefinition(new StringName("encounter.verification.effect.choose"), "选择", [effect])]) { }
    }

    // 三种尺寸的植物夹具，不新增正式卡牌（表现层夹具）。
    private sealed class PlantCard : CardDefinition
    {
        internal PlantCard(CardSize size) : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.verification.plant." + size), "植物验证" + size, GameFactions.Neutral, size, [GameElements.Wood])),
            new TagSet([GameTags.Plant])) { }
    }

    // 固定等级与轮次范围的怪物夹具（表现层夹具）。
    private sealed class TestMonster : MonsterDefinition
    {
        internal TestMonster(string suffix, int level, int minimumRound, int maximumRound) : base(
            new StringName("monster.verification.forest." + suffix), "丛林验证怪物", Array.Empty<MonsterCardEntry>(),
            maxHealth: 1, level: level, minimumRound: minimumRound, maximumRound: maximumRound) { }
    }
}
