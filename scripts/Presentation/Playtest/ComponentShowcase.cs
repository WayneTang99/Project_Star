using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Application.Economy;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 使用正式内容的只读快照展示组件边界，不接入可变试玩对局。
public sealed partial class ComponentShowcase : Control
{
    private CardDetailsView _details = null!;
    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--capture-encounters"))
        { Callable.From(CaptureEncounters).CallDeferred(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--capture-heroes"))
        { Callable.From(CaptureHeroes).CallDeferred(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--capture-gem-sockets"))
        { Callable.From(CaptureGemSockets).CallDeferred(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--capture-card-pool"))
        { Callable.From(CaptureCardPool).CallDeferred(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--capture-p4") || OS.GetCmdlineUserArgs().Contains("--capture-p6-transactions")
            || OS.GetCmdlineUserArgs().Contains("--capture-descriptions") || OS.GetCmdlineUserArgs().Contains("--capture-keywords")
            || OS.GetCmdlineUserArgs().Contains("--capture-card-states"))
        { Callable.From(CaptureTransactions).CallDeferred(); return; }
        if (OS.GetCmdlineUserArgs().Contains("--capture-p5") || OS.GetCmdlineUserArgs().Contains("--capture-p6-playback")
            || OS.GetCmdlineUserArgs().Contains("--capture-resource-bars"))
        { Callable.From(CapturePlayback).CallDeferred(); return; }
        var scroll = new ScrollContainer { Name = "Scroll" }; AddChild(scroll);
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var column = new VBoxContainer { Name = "Examples", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 12); scroll.AddChild(column);
        var back = new Button { Text = "返回试玩" }; column.AddChild(back);
        back.Pressed += () => GetTree().ChangeSceneToFile("res://Playtest.tscn");
        var original = new Button { Text = "查看原始卡面展示" }; column.AddChild(original);
        original.Pressed += () => GetTree().ChangeSceneToFile("res://scripts/Presentation/CardFace/CardFaceShowcase.tscn");
        var session = new CreateMatchService(new EntityFactory()).Create(42, 100, new PaladinHeroDefinition());
        var snapshot = MatchSnapshot.From(session);
        var top = new TopBar { CustomMinimumSize = new Vector2(0, 40) }; column.AddChild(top); top.Render(snapshot);
        var identity = new HBoxContainer(); column.AddChild(identity);
        var portrait = new ContextPortrait { CustomMinimumSize = new Vector2(240, 90) }; identity.AddChild(portrait); portrait.Render("组件展示：正式内容快照");
        var hero = new PlayerHeroPanel { CustomMinimumSize = new Vector2(180, 90) }; identity.AddChild(hero); hero.Render(snapshot.Hero);
        var progress = new PlayerProgressPanel(); identity.AddChild(progress); progress.Render(snapshot);
        var samples = new[]
        {
            MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArmguardCardDefinition())) with { Id = EntityId.New() },
            MatchDisplayQuery.FromOffer(ShopOffer.Create(new BoarCardDefinition())) with { Id = EntityId.New() },
            MatchDisplayQuery.FromOffer(ShopOffer.Create(new JudgmentHammerCardDefinition())) with { Id = EntityId.New() },
            MatchDisplayQuery.FromOffer(ShopOffer.Create(new NunCardDefinition())) with
            { Id = EntityId.New(), DisplayName = "修女 · 缺图与很长名称展示快照（不修改正式定义）",
                Illustration = new StringName("res://art/ui/card-face/artwork/missing-demo.png") },
        };
        column.AddChild(new Label { Text = "小型 / 中型 / 大型 / 缺图长名禁用态；右键查看完整详情" });
        var cards = new HBoxContainer { Name = "CardSamples" }; column.AddChild(cards);
        var adapter = new CardDisplayAdapter();
        for (var index = 0; index < samples.Length; index++)
        {
            var card = new CardItemView { Name = $"Sample{index}", CustomMinimumSize = new Vector2(130 * (int)samples[index].Size, 260), Disabled = index == 3 };
            cards.AddChild(card); card.Render(samples[index], adapter); card.DetailsRequested += ShowDetails;
        }
        var start = 0;
        var placements = samples.Select(card =>
        {
            var placement = new BoardPlacementSnapshot(card.Id, BoardZone.Battlefield, start, start + (int)card.Size);
            start = placement.EndExclusive; return placement;
        }).ToArray();
        var occupied = new BoardZoneView { Name = "ReadOnlyBoard", CustomMinimumSize = new Vector2(0, 162) };
        column.AddChild(occupied);
        occupied.Render(snapshot with { Cards = Array.AsReadOnly(samples), BoardPlacements = Array.AsReadOnly(placements) },
            BoardZone.Battlefield, false, null, "只读敌方区域");
        occupied.DetailsRequested += ShowDetails;
        var empty = new BoardZoneView { Name = "EmptyBoard", CustomMinimumSize = new Vector2(0, 230) }; column.AddChild(empty);
        empty.Render(snapshot, BoardZone.Bench, false, null, "空备战区");
        _details = new CardDetailsView { Name = "CardDetails", ZIndex = 10 }; AddChild(_details);
        Resized += ClampDetails;
        if (OS.GetCmdlineUserArgs().Contains("--capture-showcase") || OS.GetCmdlineUserArgs().Contains("--capture-p3"))
            Callable.From(Capture).CallDeferred();
    }

    public override void _ExitTree() { if (_details is not null) Resized -= ClampDetails; }
    private void ShowDetails(CardSnapshot card) => _details.ShowCard(card, GetLocalMousePosition(), Size);
    private void ClampDetails() => _details.ClampTo(Size);

    // 捕获真实遭遇图卡与商店页面，再总览全部正式原画和五档等级晶体。
    private async void CaptureEncounters()
    {
        Theme = MatchTheme.Create();
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        AddChild(root); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shell = root.GetNode<MatchShell>("MatchShell");
        var presenter = PlaytestVerification.CreatePresenter(encounterLevelOverride: 5);
        shell.Render(presenter.View);
        await Save("selection");
        presenter.ChooseEncounter(presenter.View.Choices.First(choice => choice.ShopLevel > 0).Key);
        shell.Render(presenter.View);
        await Save("shop");
        foreach (var offer in presenter.View.Offers.Take(2).ToArray()) presenter.BuyCard(offer.Index, offer.Revision);
        presenter.ContinueMatch(); shell.Render(presenter.View); await Save("build");
        presenter.ChooseEncounter(new StringName("encounter.training_ground"));
        shell.Render(presenter.View); await Save("event");
        presenter.ResolveEventOption(presenter.View.EventOptions[0].Key, presenter.View.EventRevision);
        presenter.ContinueMatch();
        // 按正式排程推进到战斗准备，避免把普通回合商店误标为回放截图。
        while (presenter.View.Page == MatchPage.EncounterChoice)
        {
            presenter.ChooseEncounter(presenter.View.Choices[0].Key);
            if (presenter.View.Page == MatchPage.Event)
                presenter.ResolveEventOption(presenter.View.EventOptions[0].Key, presenter.View.EventRevision);
            if (presenter.View.Page is MatchPage.Shop or MatchPage.Event) presenter.ContinueMatch();
        }
        if (presenter.View.Page != MatchPage.Preparation) throw new InvalidOperationException("未进入战斗准备截图页面。");
        shell.Render(presenter.View); await Save("preparation");
        presenter.StartBattle(); presenter.AdvancePlayback(.5);
        shell.Render(presenter.View); await Save("playback");
        RemoveChild(root); root.QueueFree();
        var background = new ColorRect { Color = MatchTheme.Background, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var choices = registry.Encounters.Values.OrderBy(item => item.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .Select(item => new KeyedAction(item.Attributes.Identity.Key,
                new UiAction(item.Attributes.Identity.DisplayName))
                { Illustration = item.Attributes.Identity.Illustration, ShopLevel = item is ShopEncounterDefinition shop ? shop.Level : 0,
                    Subtitle = item.Attributes.Identity.Summary,
                    Level = item switch { ShopEncounterDefinition store => store.Level, ChoiceEncounterDefinition choice => choice.Level, _ => 0 } })
            .Concat(registry.Monsters.Values.Select(item => new KeyedAction(item.Attributes.Identity.Key,
                new UiAction(item.Attributes.Identity.DisplayName)) { Illustration = item.Attributes.Identity.Illustration, Level = item.Level, Subtitle = "怪物战" })).ToArray();
        var height = (Size.Y - 110) / 3;
        for (var row = 0; row < 3; row++)
        {
            var view = new EncounterSelectionView { Position = new Vector2(24, 12 + row * height),
                Size = new Vector2(Size.X - 48, height - 12) };
            AddChild(view); view.Render(row == 0 ? "全部遭遇原画" : "", choices.Skip(row * 3).Take(3).ToArray());
        }
        var legend = new HBoxContainer { Position = new Vector2(24, Size.Y - 80) }; AddChild(legend);
        for (var level = 1; level <= 5; level++)
        {
            var crystal = new CardLevelGem { CustomMinimumSize = new Vector2(40, 52) };
            legend.AddChild(crystal); crystal.SetLevel(level);
            legend.AddChild(new Label { Text = $"{level}级商店", CustomMinimumSize = new Vector2(100, 52) });
        }
        await Save("gallery");
        GetTree().Quit();

        async System.Threading.Tasks.Task Save(string name)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            DirAccess.MakeDirRecursiveAbsolute("res://docs/quality/encounter-art");
            using var image = GetViewport().GetTexture().GetImage();
            // HDR 视口读回的是线性色彩，PNG 预览需要转换为 sRGB。
            if (GetViewport().UseHdr2D)
                for (var y = 0; y < image.GetHeight(); y++)
                    for (var x = 0; x < image.GetWidth(); x++)
                        image.SetPixel(x, y, image.GetPixel(x, y).LinearToSrgb());
            image.Convert(Image.Format.Rgba8);
            if (image.SavePng($"res://docs/quality/encounter-art/{name}-{image.GetWidth()}x{image.GetHeight()}.png") != Error.Ok)
                throw new InvalidOperationException("遭遇截图保存失败。");
        }
    }

    // 捕获真实选角入口与选中英雄的头像、资源条布局。
    private async void CaptureHeroes()
    {
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        AddChild(root); root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shell = root.GetNode<MatchShell>("MatchShell");
        var selector = shell.GetNode<HeroSelectionView>("ContextRow/ContextHost/HeroSelectionView");
        await Save("selection");
        selector.GetNode<Button>("Next").EmitSignal(Button.SignalName.Pressed);
        await Save("next");
        selector.GetNode<Button>("Previous").EmitSignal(Button.SignalName.Pressed);
        selector.GetNode<Button>("Choose").EmitSignal(Button.SignalName.Pressed);
        await Save("selected");
        GetTree().Quit();

        async System.Threading.Tasks.Task Save(string name)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            DirAccess.MakeDirRecursiveAbsolute("res://output/heroes");
            using var image = GetViewport().GetTexture().GetImage();
            if (image.SavePng($"res://output/heroes/{name}-{image.GetWidth()}x{image.GetHeight()}.png") != Error.Ok)
                throw new InvalidOperationException("英雄截图保存失败。");
        }
    }

    // 宝石展示使用冻结夹具，不向正式卡池添加宝石或获取渠道。
    private async void CaptureGemSockets()
    {
        Theme = MatchTheme.Create();
        var background = new ColorRect { Color = MatchTheme.Background, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var column = new VBoxContainer { Position = new Vector2(24, 20) }; AddChild(column);
        column.AddChild(new Label { Text = "宝石孔 UI 验证 · 深色圆孔为空 / 明亮填充为已镶嵌 · 测试宝石不属于正式内容" });
        var row = new HBoxContainer(); column.AddChild(row);
        var sample = MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArmguardCardDefinition()));
        var samples = new[]
        {
            sample,
            sample with { GemSockets = Array.AsReadOnly(new GemSnapshot?[] { new(new StringName("verification.gem.red"), "测试红宝石", "展示夹具") }) },
            sample with { GemSockets = Array.AsReadOnly(new GemSnapshot?[] { null, new(new StringName("verification.gem.blue"), "测试蓝宝石", "展示夹具"), null }) },
        };
        var adapter = new CardDisplayAdapter();
        foreach (var card in samples)
        {
            var cell = new VBoxContainer(); row.AddChild(cell);
            var face = new CardItemView { CustomMinimumSize = new Vector2(220, 310) };
            cell.AddChild(face); face.Render(card, adapter);
            cell.AddChild(new Label { Text = string.Join("\n", CardDisplayAdapter.Details(card).Split('\n')
                .Where(line => line.StartsWith("宝石孔") || line.StartsWith("孔"))) });
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute("res://output/gem-sockets");
        using var image = GetViewport().GetTexture().GetImage();
        var result = image.SavePng($"res://output/gem-sockets/sockets-{image.GetWidth()}x{image.GetHeight()}.png");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    // 直接从正式定义与插画属性生成全卡池卡面截图，检查真实加载与裁切。
    private async void CaptureCardPool()
    {
        GetWindow().ContentScaleSize = new Vector2I(1920, 1080);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var background = new ColorRect { Color = new Color("333c41"), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(background); background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var column = new VBoxContainer { Position = new Vector2(24, 20) }; AddChild(column);
        column.AddThemeConstantOverride("separation", 18);
        var registry = DefinitionRegistry.Scan(typeof(ComponentShowcase).Assembly);
        column.AddChild(new Label { Text = $"全部 {registry.Cards.Count} 张正式卡牌 · 插画属性加载 · 初始等级" });
        var grid = new GridContainer { Columns = 6 }; column.AddChild(grid);
        grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 18);
        var adapter = new CardDisplayAdapter();
        foreach (var definition in registry.Cards.Values.OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal))
        {
            var sample = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition));
            var cell = new CenterContainer { CustomMinimumSize = new Vector2(300, 286) }; grid.AddChild(cell);
            var card = new CardItemView { CustomMinimumSize = new Vector2(96 * (int)sample.Size, 276) };
            cell.AddChild(card); card.Render(sample, adapter);
        }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute("res://docs/quality/card-art");
        using var image = GetViewport().GetTexture().GetImage();
        var result = image.SavePng($"res://docs/quality/card-art/card-pool-{image.GetWidth()}x{image.GetHeight()}.png");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }

    // 用正式卡牌与同一次战斗记录捕获暂停和播放画面。
    private async void CapturePlayback()
    {
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        var shell = root.GetNode<MatchShell>("MatchShell"); root.RemoveChild(shell); root.Free(); AddChild(shell);
        shell.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var factory = new EntityFactory();
        var board = new Project_Star.Application.Board.BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new Project_Star.Application.Economy.CardEconomyService(factory, board, registry.Cards.Values);
        var create = new CreateMatchService(factory);
        var player = create.Create(42, 100, new PaladinHeroDefinition());
        var enemy = create.Create(43, 100, new PaladinHeroDefinition());
        foreach (var definition in new Project_Star.Domain.Definitions.CardDefinition[]
            { new ArmguardCardDefinition(), new NunCardDefinition(), new MilitaryBootsCardDefinition() })
            if (economy.AcquireAndPlace(player, definition, definition.InitialLevel, CardAcquisitionSource.Reward).IsFailure)
                throw new InvalidOperationException("回放截图玩家卡牌放置失败");
        foreach (var definition in new Project_Star.Domain.Definitions.CardDefinition[]
            { new BoarCardDefinition(), new ThornArmorCardDefinition(), new LightCavalryCardDefinition() })
            if (economy.AcquireAndPlace(enemy, definition, definition.InitialLevel, CardAcquisitionSource.Reward).IsFailure)
                throw new InvalidOperationException("回放截图对手卡牌放置失败");
        var before = MatchSnapshot.From(player); var opponent = MatchSnapshot.From(enemy);
        var result = new Project_Star.Application.Combat.StartBattleService(
            new Project_Star.Application.Combat.BattleSetupFactory(registry.Sets),
            new Project_Star.Domain.Combat.CombatSimulator()).StartBattle(player, enemy, 42).Value!;
        var playback = new BattlePlaybackPresenter(new Project_Star.Application.Combat.BattleResolution(before, opponent,
            result, MatchSnapshot.From(player)));
        playback.Advance(5); playback.TogglePause();
        Render(); await Save("paused");
        playback.TogglePause(); playback.Advance(5);
        Render(); await Save("playing");
        GetTree().Quit();

        void Render() => shell.Render(new MatchPageViewModel(MatchPage.BattlePlayback, "战斗回放", "正式卡牌回放展示",
            playback.Project(SideId.Player), playback.Project(SideId.Opponent),
            null, false, true, Array.Empty<KeyedAction>(), Array.Empty<KeyedAction>(), Array.Empty<ShopItemViewModel>(),
            Array.Empty<KeyedAction>(), 1, new UiAction("刷新", false), new UiAction("开始战斗", false),
            new UiAction("继续", false), new UiAction("奖励", false)) { Playback = playback.Capture() });

        async System.Threading.Tasks.Task Save(string name)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var directory = OS.GetCmdlineUserArgs().Contains("--capture-resource-bars") ? "res://output/resource-bars"
                : OS.GetCmdlineUserArgs().Contains("--capture-p6-playback") ? "res://docs/quality/ui-p6" : "res://docs/quality/ui-p5";
            DirAccess.MakeDirRecursiveAbsolute(directory);
            using var image = GetViewport().GetTexture().GetImage();
            if (image.SavePng($"{directory}/{name}-{image.GetWidth()}x{image.GetHeight()}.png") != Error.Ok)
                throw new InvalidOperationException("保存回放截图失败");
        }
    }

    private async void CaptureTransactions()
    {
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        var shell = root.GetNode<MatchShell>("MatchShell"); root.RemoveChild(shell); root.Free(); AddChild(shell);
        shell.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var session = new CreateMatchService(new EntityFactory()).Create(42, 100, new PaladinHeroDefinition());
        var snapshot = MatchDisplayQuery.Capture(session, registry.Sets);
        var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(new BoarCardDefinition())) with { Id = EntityId.New() };
        var skill = registry.Skills.Values.First();
        snapshot = snapshot with { Cards = Array.AsReadOnly(new[] { card }),
            Skills = Array.AsReadOnly(new[] { MatchDisplayQuery.FromSkill(skill, skill.InitialLevel) }),
            BoardPlacements = Array.AsReadOnly(new[] { new BoardPlacementSnapshot(card.Id, BoardZone.Battlefield, 0, 2) }),
            BattlefieldCount = 1 };
        if (OS.GetCmdlineUserArgs().Contains("--capture-p6-transactions"))
        {
            var samples = new Project_Star.Domain.Definitions.CardDefinition[] { new ArmguardCardDefinition(),
                new BoarCardDefinition(), new JudgmentHammerCardDefinition(), new NunCardDefinition(), new CathedralCardDefinition() }
                .Select(definition => MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)) with { Id = EntityId.New() }).ToArray();
            var placements = new System.Collections.Generic.List<BoardPlacementSnapshot>();
            var start = 0;
            for (var index = 0; index < samples.Length; index++)
            {
                if (index == 3) start = 0;
                var zone = index < 3 ? BoardZone.Battlefield : BoardZone.Bench;
                placements.Add(new BoardPlacementSnapshot(samples[index].Id, zone, start, start + (int)samples[index].Size));
                start += (int)samples[index].Size;
            }
            card = samples[1];
            snapshot = snapshot with { Cards = Array.AsReadOnly(samples), BoardPlacements = placements.AsReadOnly(),
                BattlefieldCount = 3, BenchCount = 2 };
        }
        var offers = new[] { new ArmguardCardDefinition() as Project_Star.Domain.Definitions.CardDefinition,
            new BoarCardDefinition(), new JudgmentHammerCardDefinition() }.Select((definition, index) =>
                new ShopItemViewModel(index, 1, new UiAction(index == 0 ? "已售罄" : "购买",
                    Enabled: index != 0, Reason: index == 0 ? "已售罄" : ""), MatchDisplayQuery.FromOffer(ShopOffer.Create(definition!)))
                    { Price = ShopOffer.Create(definition!).Price }).ToArray();
        var view = new MatchPageViewModel(MatchPage.Shop, "商店 · 交易组件", "购买价与持有价值分别显示；右键查看商品详情。",
            snapshot, null, null, true, false, Array.Empty<KeyedAction>(), Array.Empty<KeyedAction>(), Array.AsReadOnly(offers),
            Array.Empty<KeyedAction>(), 1, new UiAction("刷新（2）· 剩余 1"), new UiAction("开始战斗", false),
            new UiAction("离开商店"), new UiAction("奖励", false))
        {
            Sell = new UiAction("出售"),
            Rewards = Array.AsReadOnly(new[] {
                new RewardItemViewModel(0, 1, "卡牌 · 修女 · 1级", MatchDisplayQuery.FromOffer(ShopOffer.Create(new NunCardDefinition())), ""),
                new RewardItemViewModel(1, 1, $"技能 · {skill.Attributes.Identity.DisplayName} · {skill.InitialLevel}级", null,
                    CardDisplayAdapter.SkillDetails(MatchDisplayQuery.FromSkill(skill, skill.InitialLevel))) }),
        };
        if (OS.GetCmdlineUserArgs().Contains("--capture-keywords") || OS.GetCmdlineUserArgs().Contains("--capture-card-states"))
        {
            shell.Render(view);
            var sample = card with { DescriptionEntries = Array.AsReadOnly(new CardDescriptionEntry[]
            {
                new(CardKeywords.Activate, "攻击造成伤害，并获得护甲、治疗。"),
                new(CardKeywords.Echo, "当中毒、灼伤生效后，获得疾速、充能，施加禁锢。"),
            }), IsFlying = OS.GetCmdlineUserArgs().Contains("--capture-card-states"),
                IsBerserk = OS.GetCmdlineUserArgs().Contains("--capture-card-states") };
            shell.GetNode<CardDetailsView>("CardDetails").ShowCard(sample, new Vector2(280, 90), shell.Size);
            var item = new CardItemView();
            var tooltip = (Control)item._MakeCustomTooltip(CardDisplayAdapter.Details(sample)); item.Free();
            var tooltipPanel = new PanelContainer(); shell.AddChild(tooltipPanel);
            tooltipPanel.AddChild(tooltip); tooltipPanel.Position = new Vector2(760, 330);
            await Save(OS.GetCmdlineUserArgs().Contains("--capture-card-states") ? "card-states" : "keywords");
            GetTree().Quit(); return;
        }
        if (OS.GetCmdlineUserArgs().Contains("--capture-descriptions"))
        {
            foreach (var definition in new Project_Star.Domain.Definitions.CardDefinition[] {
                new HolyGriffinCardDefinition(), new CathedralCardDefinition(), new JewelryBagCardDefinition() })
            {
                shell.Render(view);
                var sample = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition));
                shell.GetNode<CardDetailsView>("CardDetails").ShowCard(sample, new Vector2(280, 90), shell.Size);
                await Save(sample.Key.ToString().Replace("card.", ""));
            }
            GetTree().Quit(); return;
        }
        shell.Render(view); await Save("shop");
        if (OS.GetCmdlineUserArgs().Contains("--capture-p6-transactions"))
        {
            var scroll = shell.GetNode<ScrollContainer>("ContextRow/ContextHost/ShopView/OfferScroll");
            scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
            shell.GetNode<Button>("ContextRow/ContextHost/ShopView/OfferScroll/Offers/Offer2/Actions/Buy").GrabFocus();
            await Save("shop-last");
        }
        shell.Render(view with { SelectedCardId = card.Id });
        shell.GetNode<CardDetailsView>("CardDetails").ShowSelected(card, snapshot.MatchId, view.Sell,
            new Vector2(shell.Size.X / 2, 80), shell.Size);
        await Save("sale");
        shell.Render(view); shell.GetNode<HeroDetailsView>("HeroDetails").ShowSection(HeroSection.Rewards, shell.Size); await Save("rewards");
        GetTree().Quit();

        async System.Threading.Tasks.Task Save(string name)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var directory = OS.GetCmdlineUserArgs().Contains("--capture-card-states") ? "res://output"
                : OS.GetCmdlineUserArgs().Contains("--capture-keywords") ? "res://docs/quality/card-keywords"
                : OS.GetCmdlineUserArgs().Contains("--capture-descriptions") ? "res://docs/quality/card-description"
                : OS.GetCmdlineUserArgs().Contains("--capture-p6-transactions") ? "res://docs/quality/ui-p6" : "res://docs/quality/ui-p4";
            DirAccess.MakeDirRecursiveAbsolute(directory);
            using var image = GetViewport().GetTexture().GetImage();
            var result = image.SavePng($"{directory}/{name}-{image.GetWidth()}x{image.GetHeight()}.png");
            if (result != Error.Ok) throw new InvalidOperationException($"保存交易截图失败：{result}");
        }
    }

    private async void Capture()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (OS.GetCmdlineUserArgs().Contains("--capture-p3"))
        {
            var scroll = GetNode<ScrollContainer>("Scroll");
            scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var directory = OS.GetCmdlineUserArgs().Contains("--capture-p3") ? "res://docs/quality/ui-p3" : "res://docs/quality/ui-p2";
        DirAccess.MakeDirRecursiveAbsolute(directory);
        var image = GetViewport().GetTexture().GetImage();
        var result = image.SavePng($"{directory}/showcase-{image.GetWidth()}x{image.GetHeight()}.png");
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
