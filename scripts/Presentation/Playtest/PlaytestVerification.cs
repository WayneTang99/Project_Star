using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Encounters;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// Meaningful query and flow regression checks, executed by the existing Godot verification scene.
internal static class PlaytestVerification
{
    // 遭遇与怪物原画资源贯穿排程快照，商店等级在选前与进入后保持一致。
    public static bool EncounterArtwork()
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        if (registry.Encounters.Values.Any(item => string.IsNullOrWhiteSpace(item.Attributes.Identity.Summary))) return false;
        var paths = registry.Encounters.Values.Select(item => item.Attributes.Identity.Illustration)
            .Concat(registry.Monsters.Values.Select(item => item.Attributes.Identity.Illustration)).ToArray();
        if (paths.Any(path => path.IsEmpty || !ResourceLoader.Exists(path.ToString())
            || GD.Load<Texture2D>(path.ToString()) is null) || paths.Distinct().Count() != paths.Length) return false;
        var presenter = CreatePresenter();
        if (presenter.View.Choices.Any(choice => choice.Illustration.IsEmpty || choice.Level < 1)) return false;
        var shop = presenter.View.Choices.First(choice => choice.ShopLevel > 0);
        var snapshot = presenter.View.Player!.EncounterChoices.Single(choice => choice.Key == shop.Key);
        if (snapshot.Illustration != shop.Illustration || snapshot.ShopLevel != shop.ShopLevel
            || snapshot.Level != shop.Level || shop.Level != shop.ShopLevel
            || snapshot.Summary != shop.Subtitle || shop.Subtitle.Length == 0) return false;
        presenter.ChooseEncounter(shop.Key);
        if (presenter.View.ShopLevel != shop.ShopLevel || presenter.View.ContextIllustration != shop.Illustration
            || presenter.View.EncounterLevel != shop.Level) return false;
        presenter.Reset();
        if (!presenter.View.ContextIllustration.IsEmpty || presenter.View.ShopLevel != 0 || presenter.View.EncounterLevel != 0) return false;
        var session = new CreateMatchService(new EntityFactory()).Create(42, 100, new PaladinHeroDefinition());
        session.Progress.Turn = 4;
        var result = new EncounterScheduler(registry, allowIncompleteMonsterChoices: true).Generate(session);
        return result.IsSuccess && result.Value!.All(choice => choice.Kind == EncounterKind.Monster
            && choice.Illustration == registry.Monsters[choice.Key].Attributes.Identity.Illustration && choice.ShopLevel == 0
            && choice.Level == registry.Monsters[choice.Key].Level);
    }

    // 真实图卡提交选择，刷新替换旧按钮后不再重复操作。
    public static bool EncounterArtworkScene(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        owner.AddChild(scene);
        try
        {
            var presenter = (MatchPresenter)typeof(MinimalPlaytest).GetField("_presenter",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(scene)!;
            presenter.SelectHero(new StringName("hero.paladin"));
            var index = presenter.View.Choices.ToList().FindIndex(choice => choice.ShopLevel > 0);
            var choice = presenter.View.Choices[index];
            var buttons = scene.GetNode<HBoxContainer>("MatchShell/ContextRow/ContextHost/EncounterChoiceView/Choices");
            for (var item = 0; item < buttons.GetChildCount(); item++)
            {
                var tile = buttons.GetChild<Button>(item);
                if (!tile.FindChild("EncounterLevelCrystal", true, false)!.IsClass("Control")
                    || !tile.FindChild("EncounterLevel", true, false)!.Get("text").AsString()
                        .Contains(presenter.View.Choices[item].Level.ToString())) return false;
            }
            var button = buttons.GetChild<Button>(index);
            if (button.GetNode<TextureRect>("Illustration").Texture is null
                || !button.TooltipText.Contains($"{choice.ShopLevel}级商店")) return false;
            button.EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Page != MatchPage.Shop || presenter.View.ShopLevel != choice.ShopLevel
                || !scene.GetNode<CardLevelGem>("MatchShell/ContextRow/ContextHost/ShopView/ShopLevelCrystal").Visible) return false;
            var turn = presenter.View.Player!.Turn;
            button.EmitSignal(Button.SignalName.Pressed);
            return presenter.View.Player.Turn == turn;
        }
        finally { owner.RemoveChild(scene); scene.Free(); }
    }

    // 真实测试入口各类遭遇固定5级，普通排程与正式定义继续使用原等级。
    public static bool TestEncounterLevels(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        owner.AddChild(scene);
        try
        {
            var presenter = (MatchPresenter)typeof(MinimalPlaytest).GetField("_presenter",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(scene)!;
            presenter.SelectHero(new StringName("hero.paladin"));
            var kinds = new System.Collections.Generic.HashSet<EncounterKind>();
            for (var turn = 1; turn <= 8; turn++)
            {
                var choices = presenter.View.Player!.EncounterChoices;
                if (choices.Count == 0 || choices.Any(choice => choice.Level != 5
                    || choice.Kind == EncounterKind.Shop && choice.ShopLevel != 5)) return false;
                var selected = choices.FirstOrDefault(choice => turn == 2 && choice.Kind == EncounterKind.Other)
                    ?? choices.FirstOrDefault(choice => choice.Kind == EncounterKind.Shop) ?? choices[0];
                kinds.Add(selected.Kind); presenter.ChooseEncounter(selected.Key);
                if (presenter.View.EncounterLevel != 5) return false;
                if (presenter.View.Page == MatchPage.Shop)
                {
                    if (presenter.View.ShopLevel != 5 || !presenter.View.Refresh.Text.Contains("10 金币")
                        || presenter.View.Offers.Any(offer => offer.Card.Level > 5)) return false;
                }
                else if (presenter.View.Page == MatchPage.Event)
                    presenter.ResolveEventOption(presenter.View.EventOptions[0].Key, presenter.View.EventRevision);
                else if (presenter.View.Page == MatchPage.Preparation)
                { presenter.StartBattle(); presenter.SkipPlayback(); }
                presenter.ContinueMatch();
            }
            var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
            var session = new CreateMatchService(new EntityFactory()).Create(42, 100, new PaladinHeroDefinition());
            var normal = new EncounterScheduler(registry, allowIncompleteMonsterChoices: true);
            if (normal.Generate(session).Value!.Any(choice => choice.Level == 5)) return false;
            session.Progress.Turn = 4;
            if (normal.Generate(session).Value!.Any(choice => choice.Level != registry.Monsters[choice.Key].Level)) return false;
            session.Progress.Turn = 8;
            return normal.Generate(session).Value!.Single().Level == 0 && kinds.Count == 4;
        }
        finally { owner.RemoveChild(scene); scene.Free(); }
    }

    // 完整购买预检空间与余额；满盘合并可成功，失败与重复提交不留交易状态。
    public static bool CompletePurchases()
    {
        var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(new EntityFactory(), board);
        var session = new MatchSession(42, 100, boardCapacity: 1);
        var armguard = economy.AcquireAndPlace(session, new ArmguardCardDefinition(), 1, CardAcquisitionSource.Reward).Value!;
        economy.AcquireAndPlace(session, new NunCardDefinition(), 4, CardAcquisitionSource.Reward);
        var failed = ShopOffer.Create(new BoarCardDefinition());
        var random = session.Random.State;
        if (economy.BuyAndPlace(session, failed).IsSuccess || failed.IsSold || session.Player.Wealth != 100
            || session.Player.Inventory.Cards.Count != 2 || session.Random.State != random) return false;
        var merge = ShopOffer.Create(new ArmguardCardDefinition());
        var bought = economy.BuyAndPlace(session, merge);
        if (bought.IsFailure || bought.Value!.Id != armguard.Id || bought.Value.CurrentLevel != 2
            || !session.Board.Contains(armguard.Id) || !merge.IsSold || session.Player.Inventory.Cards.Count != 2) return false;
        var wealth = session.Player.Wealth;
        if (economy.BuyAndPlace(session, merge).IsSuccess || session.Player.Wealth != wealth) return false;
        var empty = new MatchSession(42);
        var unaffordable = ShopOffer.Create(new ArmguardCardDefinition());
        return economy.BuyAndPlace(empty, unaffordable).IsFailure && !unaffordable.IsSold
            && empty.Player.Inventory.Cards.Count == 0 && empty.Board.Battlefield.Count == 0;
    }

    // 连续合并释放的占格参与预检，原先未放置的目标也必须获得棋盘位置。
    public static bool MergePlacementReleasedSpace()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var session = new MatchSession(42, 100, boardCapacity: 1);
        var target = economy.AcquireCard(session, new ArmguardCardDefinition(), 1, CardAcquisitionSource.Reward).Value!;
        var consumed = economy.AcquireCard(session, new ArmguardCardDefinition(), 2, CardAcquisitionSource.Reward).Value!;
        board.PlaceCard(session, consumed.Id, BoardZone.Battlefield, 0);
        economy.AcquireAndPlace(session, new NunCardDefinition(), 4, CardAcquisitionSource.Reward);
        var bought = economy.BuyAndPlace(session, ShopOffer.Create(new ArmguardCardDefinition()));
        return bought.IsSuccess && bought.Value!.Id == target.Id && bought.Value.CurrentLevel == 3
            && session.Board.Contains(target.Id) && !session.Board.Contains(consumed.Id)
            && session.Player.Inventory.Find(consumed.Id) is null && session.Player.Inventory.Cards.Count == 2;
    }

    // 出售溢出或缺少奖励依赖时完整保留状态；成功按现值回补且只能执行一次。
    public static bool CompleteSales()
    {
        var registry = Registry(); var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(new EntityFactory(), board, registry.Cards.Values);
        var session = new MatchSession(42, int.MaxValue, boardCapacity: 1);
        var card = economy.AcquireAndPlace(session, new ArmguardCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        var rng = session.Random.State;
        if (economy.SellFromBoard(session, card.Id).IsSuccess || !session.Board.Contains(card.Id)
            || session.Player.Wealth != int.MaxValue || session.Random.State != rng) return false;
        session.Player.TrySpendWealth(100);
        card.Attributes.Persistent.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.Value, 9));
        var value = MatchSnapshot.From(session).Cards.Single().Value;
        var sold = economy.SellFromBoard(session, card.Id);
        if (sold.IsFailure || sold.Value != value || session.Board.Contains(card.Id) || session.Player.Inventory.Find(card.Id) is not null
            || session.Player.Wealth != int.MaxValue - 100 + value || economy.SellFromBoard(session, card.Id).IsSuccess) return false;
        var rewardSession = new MatchSession(42, 100, boardCapacity: 1);
        var bag = economy.AcquireAndPlace(rewardSession, new JewelryBagCardDefinition(), 2, CardAcquisitionSource.Reward).Value!;
        var missing = new CardEconomyService(new EntityFactory(), board);
        if (missing.SellFromBoard(rewardSession, bag.Id).IsSuccess || !rewardSession.Board.Contains(bag.Id)
            || rewardSession.Player.Wealth != 100) return false;
        var sale = economy.SellFromBoard(rewardSession, bag.Id);
        return sale.IsSuccess && !rewardSession.Board.Contains(bag.Id) && rewardSession.Player.Inventory.Cards.Count == 1
            && MatchSnapshot.From(rewardSession).Cards.Single().Level == 2 && rewardSession.Board.Battlefield.Count == 1;
    }

    // 领取指定技能不受满盘阻挡；卡牌失败保留奖励，旧版本与旧对局提交不能领取。
    public static bool RewardSelectionAndStaleSale()
    {
        var presenter = CreatePresenter(mediumShop: true);
        var session = (MatchSession)typeof(MatchPresenter).GetField("_player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(presenter)!;
        var registry = Registry();
        var skill = registry.Skills.Values.First();
        session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card, new StringName("card.boar"), "野猪", 1));
        session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Skill, skill.Attributes.Identity.Key, skill.Attributes.Identity.DisplayName, skill.InitialLevel));
        presenter.RefreshView();
        var reward = presenter.View.Rewards[1];
        if (reward.Card is not null || !reward.Details.Contains(skill.Attributes.Identity.DisplayName)) return false;
        presenter.ClaimReward(session.Id, reward.Index, reward.Revision);
        presenter.ClaimReward(session.Id, 0, reward.Revision);
        if (presenter.View.Player!.Skills.Count != 1 || presenter.View.Rewards.Count != 1) return false;
        var current = presenter.View.Rewards[0]; presenter.ClaimReward(session.Id, current.Index, current.Revision);
        var card = presenter.View.Player!.Cards.Single();
        var placement = presenter.View.Player.BoardPlacements.Single();
        presenter.OnBoardSlot(placement.Zone, placement.Start);
        presenter.SellCard(session.Id, card.Id, card.Level, card.Value + 1);
        if (presenter.View.Player!.Cards.Count != 1) return false;
        var wealth = presenter.View.Player.Wealth;
        presenter.SellCard(session.Id, card.Id, card.Level, card.Value);
        presenter.SellCard(session.Id, card.Id, card.Level, card.Value);
        if (presenter.View.Player!.Cards.Count != 0 || presenter.View.Player.Wealth != wealth + card.Value) return false;
        presenter.Reset(); presenter.SelectHero(new StringName("hero.paladin"));
        presenter.ClaimReward(session.Id, current.Index, current.Revision);
        return presenter.View.Player!.Cards.Count == 0;
    }

    // 卡牌奖励被满盘阻挡时，可单独领取列表后面的技能，失败不改随机与资源。
    public static bool FullBoardRewardSelection()
    {
        var registry = Registry(); var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var claims = new MonsterRewardClaimService(registry, economy, new SkillAcquisitionService(factory), board);
        var session = new MatchSession(42, 100, boardCapacity: 1);
        economy.AcquireAndPlace(session, new ArmguardCardDefinition(), 4, CardAcquisitionSource.Reward);
        economy.AcquireAndPlace(session, new NunCardDefinition(), 4, CardAcquisitionSource.Reward);
        var skill = registry.Skills.Values.First();
        session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card, new StringName("card.boar"), "野猪", 1));
        session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Skill, skill.Attributes.Identity.Key, skill.Attributes.Identity.DisplayName, skill.InitialLevel));
        var random = session.Random.State;
        if (claims.Claim(session, 0).IsSuccess || session.PendingMonsterRewards.Count != 2
            || session.Player.Wealth != 100 || session.Random.State != random || session.Player.Inventory.Cards.Count != 2) return false;
        return claims.Claim(session, 1).IsSuccess && session.PendingMonsterRewards.Count == 1
            && session.PendingMonsterRewards[0].Kind == MonsterRewardKind.Card && session.Player.Skills.Items.Count == 1
            && session.Player.Inventory.Cards.Count == 2 && session.Random.State == random;
    }

    // 真实子视图按钮连通购买、出售确认、指定奖励与重开，旧按钮解绑不重复交易。
    public static bool TransactionScene(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        owner.AddChild(scene);
        try
        {
            var presenter = (MatchPresenter)typeof(MinimalPlaytest).GetField("_presenter", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(scene)!;
            presenter.SelectHero(new StringName("hero.paladin"));
            var shop = presenter.View.Player!.EncounterChoices.First(item => item.Kind == EncounterKind.Shop);
            presenter.ChooseEncounter(shop.Key);
            var offer = presenter.View.Offers.First(item => item.Action.Enabled);
            scene.GetNode<HBoxContainer>("MatchShell/ContextRow/ContextHost/ShopView/OfferScroll/Offers")
                .GetChild<Node>(offer.Index).GetNode<Button>("Actions/Buy").EmitSignal(Button.SignalName.Pressed);
            var card = presenter.View.Player!.Cards.Single();
            var placement = presenter.View.Player.BoardPlacements.Single();
            var zone = placement.Zone == BoardZone.Battlefield ? "BattlefieldRow" : "BenchRow";
            scene.GetNode<Button>($"MatchShell/{zone}/Content/Board/Card_{card.Id.Value:N}").EmitSignal(Button.SignalName.Pressed);
            var details = scene.GetNode<CardDetailsView>("MatchShell/CardDetails");
            var sale = details.GetNode<Button>("Content/Sell");
            if (!details.Visible || sale.Disabled || !sale.Text.Contains($"回补 {card.Value}")) return false;
            var wealth = presenter.View.Player.Wealth;
            sale.EmitSignal(Button.SignalName.Pressed); sale.EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Player!.Cards.Count != 0 || presenter.View.Player.Wealth != wealth + card.Value || details.Visible) return false;
            var hero = scene.GetNode<PlayerHeroPanel>("MatchShell/BenchRow/Hero/PlayerHeroPanel");
            hero.GetChildren().OfType<HFlowContainer>().Single().GetChild<Button>(2).EmitSignal(Button.SignalName.Pressed);
            var rewards = scene.GetNode<HeroDetailsView>("MatchShell/HeroDetails");
            if (!rewards.Visible) return false;
            var session = (MatchSession)typeof(MatchPresenter).GetField("_player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(presenter)!;
            var skill = Registry().Skills.Values.First();
            session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card, new StringName("card.boar"), "野猪", 1));
            session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Skill, skill.Attributes.Identity.Key, skill.Attributes.Identity.DisplayName, skill.InitialLevel));
            // 终局棋盘只读，但应用用例允许领取保留的末局奖励。
            session.Status = MatchStatus.Won;
            typeof(MatchPresenter).GetField("_page", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(presenter, MatchPage.MatchEnded);
            presenter.RefreshView();
            if (presenter.View.BoardEnabled) return false;
            var items = rewards.GetNode<VBoxContainer>("Content/Scroll/Items");
            var old = items.GetNode<Button>("Claim1"); old.EmitSignal(Button.SignalName.Pressed); old.EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Player!.Skills.Count != 1 || presenter.View.Rewards.Count != 1 || !rewards.Visible) return false;
            items.GetNode<Button>("Claim0").EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Player!.Cards.Count != 1 || presenter.View.Rewards.Count != 0 || !rewards.Visible) return false;
            presenter.Reset();
            return !rewards.Visible && !details.Visible && presenter.View.Player is null;
        }
        finally { owner.RemoveChild(scene); scene.Free(); }
    }

    // 注入引擎输入而非直接调用拖放方法，覆盖原生阈值与释放事件顺序。
    public static async Task<bool> NativeDrag(Control owner)
    {
        var session = new MatchSession(42, 100, boardCapacity: 6);
        var economy = new CardEconomyService(new EntityFactory());
        var service = new BoardService(new BoardPlacementSolver());
        var card = economy.AcquireCard(session, new BoarCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        service.PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        var source = new BoardZoneView { Position = new Vector2(100, 0), Size = new Vector2(600, 220), ZIndex = 50 };
        var target = new BoardZoneView { Position = new Vector2(100, 280), Size = new Vector2(600, 220), ZIndex = 50 };
        var viewport = new SubViewport { Size = new Vector2I(800, 600), Disable3D = true, HandleInputLocally = true };
        owner.AddChild(viewport); viewport.AddChild(source); viewport.AddChild(target);
        var commits = 0; var clicks = 0; var queries = 0; var lastStart = -1;
        bool Fail(string stage) { GD.Print($"拖拽阶段 {stage}：点击 {clicks}，提交 {commits}，预览 {queries}/{lastStart}，拖拽 {viewport.GuiIsDragging()}"); return false; }
        void Refresh()
        {
            var capture = MatchSnapshot.From(session);
            source.Render(capture, BoardZone.Battlefield, true, null, "原生拖拽来源");
            target.Render(capture, BoardZone.Bench, true, null, "原生拖拽目标");
        }
        foreach (var board in new[] { source, target })
        {
            board.PreviewRequested = (drag, zone, start) => { queries++; lastStart = start; return service.PreviewPlaceCard(session, drag.CardId, zone, start); };
            board.DropRequested += (drag, zone, start) => { commits++; service.PlaceCard(session, drag.CardId, zone, start); Refresh(); };
            board.SlotPressed += _ => clicks++;
        }
        async Task Send(InputEvent input)
        {
            if (input is InputEventKey) { Input.ParseInputEvent(input); Input.FlushBufferedEvents(); }
            viewport.PushInput(input, true);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        InputEventMouseButton Button(Vector2 point, bool pressed) => new()
        { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = pressed,
            ButtonMask = pressed ? MouseButtonMask.Left : 0 };
        InputEventMouseMotion Motion(Vector2 point, Vector2 relative, bool pressed) => new()
        { Position = point, GlobalPosition = point, Relative = relative, ButtonMask = pressed ? MouseButtonMask.Left : 0 };
        try
        {
            viewport.NotifyMouseEntered();
            Refresh(); await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            var item = source.GetNode<CardItemView>("Card_" + card.Id.Value.ToString("N"));
            var press = item.GlobalPosition + new Vector2(item.Size.X * .7f, 50);
            await Send(Motion(press, Vector2.Zero, false)); await Send(Button(press, true));
            await Send(Motion(press + new Vector2(3, 0), new Vector2(3, 0), true));
            if (viewport.GuiIsDragging()) return Fail("阈值");
            await Send(Button(press + new Vector2(3, 0), false));
            if (clicks != 1 || commits != 0) return Fail("点击");
            clicks = 0;
            await Send(Button(press, true));
            var destination = target.GetNode<Button>("Slot3").GlobalPosition + new Vector2(40, 50);
            await Send(Motion(destination, destination - press, true));
            if (!viewport.GuiIsDragging() || commits != 0) return Fail("开始");
            await Send(Motion(destination + Vector2.Right, Vector2.Right, true));
            await Send(Button(destination, false));
            if (commits != 1 || clicks != 0 || session.Board.Bench.Find(card.Id)?.Start != 2) return Fail("释放");
            item = target.GetNode<CardItemView>("Card_" + card.Id.Value.ToString("N"));
            press = item.GlobalPosition + new Vector2(30, 50);
            await Send(Motion(press, Vector2.Zero, false)); await Send(Button(press, true));
            await Send(Motion(press + new Vector2(30, 0), new Vector2(30, 0), true));
            await Send(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
            await Send(Button(press + new Vector2(30, 0), false));
            return !viewport.GuiIsDragging() && commits == 1 && clicks == 0 && session.Board.Bench.Find(card.Id)?.Start == 2;
        }
        finally
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
            Input.FlushBufferedEvents(); owner.RemoveChild(viewport); viewport.Free();
        }
    }

    // 预览必须无副作用，并在提交时重新检查最新阻挡条件。
    public static bool BoardPreviewAndCommit()
    {
        var session = new MatchSession(42, 100, boardCapacity: 6);
        var economy = new CardEconomyService(new EntityFactory());
        var service = new BoardService(new BoardPlacementSolver());
        var small = economy.AcquireCard(session, new ArmguardCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        var medium = economy.AcquireCard(session, new BoarCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        var large = economy.AcquireCard(session, new JudgmentHammerCardDefinition(), 3, CardAcquisitionSource.Reward).Value!.Card;
        service.PlaceCard(session, small.Id, BoardZone.Battlefield, 0);
        service.PlaceCard(session, medium.Id, BoardZone.Battlefield, 2);
        service.PlaceCard(session, large.Id, BoardZone.Bench, 0);
        var before = MatchSnapshot.From(session); var random = session.Random.State;
        var preview = service.PreviewPlaceCard(session, large.Id, BoardZone.Battlefield, 1);
        if (preview.IsFailure || preview.Value!.AffectedCards != 1 || preview.Value.Moves.Count != 2
            || !before.BoardPlacements.SequenceEqual(MatchSnapshot.From(session).BoardPlacements)
            || session.Random.State != random || session.Player.Wealth != before.Wealth) return false;
        service.SetPushable(session, medium.Id, false);
        var blocked = service.PlaceCard(session, large.Id, BoardZone.Battlefield, 1);
        if (blocked.IsSuccess || !blocked.Failure!.Message.Contains("不可推挤")
            || !before.BoardPlacements.SequenceEqual(MatchSnapshot.From(session).BoardPlacements)) return false;
        service.SetPushable(session, medium.Id, true);
        var committed = service.PlaceCard(session, large.Id, BoardZone.Battlefield, 1);
        if (committed.IsFailure || preview.Value.Moves.Any(move => session.Board.GetZone(move.ToZone).Find(move.CardId)?.Start != move.ToStart)) return false;
        if (service.PreviewPlaceCard(session, large.Id, BoardZone.Battlefield, 0).IsFailure) return false;
        var extra = economy.AcquireCard(session, new NunCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        return service.PreviewPlaceCard(session, extra.Id, BoardZone.Battlefield, 0).IsFailure
            && service.PreviewPlaceCard(session, large.Id, BoardZone.Battlefield, 4).Failure!.Message.Contains("边界");
    }

    // 使用真实共享棋盘组件验证抓取偏移、只读区域与跨区提交。
    public static bool DragTargets(Control owner)
    {
        var session = new MatchSession(42, 100, boardCapacity: 6);
        var economy = new CardEconomyService(new EntityFactory());
        var service = new BoardService(new BoardPlacementSolver());
        var card = economy.AcquireCard(session, new BoarCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        service.PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
        var board = new BoardZoneView { Size = new Vector2(600, 250) }; owner.AddChild(board);
        try
        {
            var before = MatchSnapshot.From(session);
            board.Render(before, BoardZone.Bench, true, null, "备战");
            board.PreviewRequested = (drag, zone, start) => service.PreviewPlaceCard(session, drag.CardId, zone, start);
            var commits = 0;
            board.DropRequested += (drag, zone, start) => { commits++; service.PlaceCard(session, drag.CardId, zone, start); };
            using var payload = new BoardDragData(session.Id, card.Id, 1, 2);
            Variant data = payload;
            var point = new Vector2(350, 80);
            if (!board._CanDropData(point, data) || !before.BoardPlacements.SequenceEqual(MatchSnapshot.From(session).BoardPlacements)) return false;
            board._DropData(new Vector2(350, 10), data);
            if (commits != 0) return false;
            board._DropData(point, data);
            if (commits != 1 || session.Board.Bench.Find(card.Id)?.Start != 2 || session.Board.Battlefield.Find(card.Id) is not null) return false;
            board.Render(MatchSnapshot.From(session), BoardZone.Bench, false, null, "只读");
            if (board._CanDropData(point, data)) return false;
            board.Render(MatchSnapshot.From(new MatchSession(43)), BoardZone.Bench, true, null, "新对局");
            return !board._CanDropData(point, data);
        }
        finally { owner.RemoveChild(board); board.Free(); }
    }

    // 预览与取消不移动卡牌，旧对局的拖拽身份不能操作新对局。
    public static bool DragPresenterState()
    {
        var presenter = CreatePresenter(mediumShop: true);
        presenter.ChooseEncounter(new StringName("encounter.shop.medium"));
        var offer = presenter.View.Offers.First(item => item.Action.Enabled);
        presenter.BuyCard(offer.Index, offer.Revision);
        var card = presenter.View.Player!.Cards[0]; var placement = presenter.View.Player.BoardPlacements[0];
        presenter.OnBoardSlot(placement.Zone, placement.Start);
        var selected = presenter.View;
        var result = presenter.PreviewMove(selected.Player!.MatchId, card.Id, BoardZone.Bench, 0);
        if (result.IsFailure || !ReferenceEquals(selected, presenter.View)) return false;
        presenter.CancelSelection();
        if (presenter.View.SelectedCardId is not null || !selected.Player.BoardPlacements.SequenceEqual(presenter.View.Player!.BoardPlacements)) return false;
        presenter.Reset(); presenter.SelectHero(new StringName("hero.paladin"));
        var current = presenter.View.Player!;
        presenter.MoveCard(selected.Player.MatchId, card.Id, BoardZone.Bench, 0);
        return presenter.View.Player!.MatchId == current.MatchId && presenter.View.Player.Cards.Count == 0
            && presenter.View.Player.Wealth == current.Wealth;
    }

    // 原生GUI验证四种窗口的几何、选择保留、开发工具和键盘移动。
    public static async Task<bool> WindowsAndKeyboard(Control owner)
    {
        var presenter = CreatePresenter();
        presenter.ChooseEncounter(presenter.View.Choices.Single(choice => choice.Key == new StringName("encounter.shop.small")).Key);
        var offer = presenter.View.Offers[0]; presenter.BuyCard(offer.Index, offer.Revision);
        var placement = presenter.View.Player!.BoardPlacements.First();
        presenter.OnBoardSlot(placement.Zone, placement.Start);
        var capture = presenter.View;
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        var shell = root.GetNode<MatchShell>("MatchShell"); root.RemoveChild(shell); root.Free();
        var viewport = new SubViewport { Size = new Vector2I(1280, 720), Disable3D = true, HandleInputLocally = true };
        owner.AddChild(viewport); viewport.AddChild(shell);
        shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        presenter.ViewChanged += shell.Render;
        shell.BoardSlotPressed += presenter.OnBoardSlot;
        bool Fail(string stage) { GD.Print($"窗口验证失败：{stage}"); return false; }
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        try
        {
            viewport.NotifyMouseEntered(); shell.Render(capture);
            var board = shell.GetNode<BoardZoneView>("BattlefieldRow/Content/Board");
            var item = board.GetNode<CardItemView>("Card_" + placement.CardId.Value.ToString("N"));
            var identity = item.GetInstanceId();
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080),
                new Vector2I(2560, 1440), new Vector2I(2560, 1080), new Vector2I(1280, 720) })
            {
                viewport.Size = size; shell.Size = size;
                await Frame(); await Frame();
                if (!ReferenceEquals(capture, presenter.View) || presenter.View.SelectedCardId != placement.CardId
                    || item.GetInstanceId() != identity || !item.GetNode<Panel>("SelectionOutline").Visible)
                    return Fail("Resize改变快照或卡牌身份");
                foreach (var path in new[] { "ContextRow/ContextHost", "BattlefieldRow/Content", "BenchRow/Content" })
                {
                    var cell = shell.GetNode<Control>(path);
                    if (cell.GlobalPosition.X < 0 || cell.GlobalPosition.X + cell.Size.X > shell.Size.X + 1
                        || cell.GlobalPosition.Y + cell.Size.Y > shell.Size.Y + 1) return Fail("主区域越界");
                }
                var hero = shell.GetNode<PlayerHeroPanel>("BenchRow/Hero/PlayerHeroPanel");
                var name = hero.GetNode<Label>("Identity/HeroName");
                var host = shell.GetNode<Control>("ContextRow/ContextHost");
                if (name.Size.X < 120 || name.Text != capture.Player!.Hero!.DisplayName
                    || hero.GlobalPosition.X + hero.Size.X >= host.GlobalPosition.X)
                    return Fail("英雄姓名被压缩或侧栏遮挡主内容");
                var offers = shell.GetNode<HBoxContainer>("ContextRow/ContextHost/ShopView/OfferScroll/Offers");
                var buys = offers.GetChildren().OfType<Control>().Select(row => row.GetNode<Button>("Actions/Buy")).ToArray();
                if (buys.Any(buy => buy.Size.X < 100 || buy.GlobalPosition.Y + buy.Size.Y > host.GlobalPosition.Y + host.Size.Y + 1)
                    || buys.Any(buy => Mathf.Abs(buy.GlobalPosition.Y - buys[0].GlobalPosition.Y) > 1))
                    return Fail("商品未横向排列或购买按钮被裁切");
                foreach (var button in shell.GetNode<LeavePanel>("ContextRow/Leave/LeaveScroll/LeavePanel")
                    .GetChildren().OfType<Button>().Where(button => button.Visible))
                    if (button.Size.X < 40) return Fail("阶段按钮被压缩");
                if (item.Position.X < 0 || item.Position.X + item.Size.X > board.Size.X + 1
                    || item.Position.Y + item.Size.Y > board.Size.Y + 1) return Fail("卡牌越界");
                var details = shell.GetNode<CardDetailsView>("CardDetails");
                if (details.Position.X + details.Size.X > shell.Size.X + 1 || details.Position.Y + details.Size.Y > shell.Size.Y + 1)
                    return Fail("详情越界");
                item.GrabFocus();
                viewport.PushInput(new InputEventKey { Keycode = Key.Tab, Pressed = true }, true);
                viewport.PushInput(new InputEventKey { Keycode = Key.Tab, Pressed = false }, true);
                await Frame();
                if (viewport.GuiGetFocusOwner() is not Button focus || focus == item) return Fail("Tab焦点未移动");
            }
            var leave = shell.GetNode<LeavePanel>("ContextRow/Leave/LeaveScroll/LeavePanel");
            if (leave.GetNode<Button>("Verification").Visible) return Fail("开发入口未折叠");
            leave.GetNode<Button>("Developer").EmitSignal(Button.SignalName.Pressed);
            if (!leave.GetNode<Button>("Verification").Visible || !ReferenceEquals(capture, presenter.View))
                return Fail("开发展开修改对局或未显示入口");
            var target = shell.GetNode<Button>("BenchRow/Content/Board/Slot0"); target.GrabFocus();
            viewport.PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true }, true);
            viewport.PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false }, true);
            await Frame(); await Frame();
            if (!presenter.View.Player!.BoardPlacements.Any(card => card.CardId == placement.CardId && card.Zone == BoardZone.Bench && card.Start == 0)
                || presenter.View.SelectedCardId is not null || presenter.View.Player.Wealth != capture.Player!.Wealth)
                return Fail("Enter未完成移动或资源变化");
            presenter.Reset(); await Frame();
            return presenter.View.Player is null && !board.Visible;
        }
        finally
        {
            presenter.ViewChanged -= shell.Render; shell.BoardSlotPressed -= presenter.OnBoardSlot;
            owner.RemoveChild(viewport); viewport.Free();
        }
    }

    // 单独装载界面骨架，验证渲染无业务依赖、页面互斥及旧按钮解绑。
    public static bool IsolatedComponents(Control owner)
    {
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        var shell = root.GetNode<MatchShell>("MatchShell");
        root.RemoveChild(shell); root.Free();
        shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        shell.Size = new Vector2(1280, 720); owner.AddChild(shell);
        try
        {
            var presenter = CreatePresenter(mediumShop: true);
            presenter.ChooseEncounter(new StringName("encounter.shop.medium"));
            var capture = presenter.View;
            var host = shell.GetNode<Control>("ContextRow/ContextHost");
            foreach (var page in Enum.GetValues<MatchPage>())
            {
                shell.Render(capture with { Page = page, Enemy = capture.Player,
                    EnemyVisible = page is MatchPage.Preparation or MatchPage.BattlePlayback });
                if (host.GetChildren().OfType<Control>().Count(view => view.Visible) != 1) return false;
            }
            shell.Render(capture);
            var offers = host.GetNode<HBoxContainer>("ShopView/OfferScroll/Offers");
            var old = offers.GetChild<Node>(0).GetNode<Button>("Actions/Buy");
            var count = 0; var index = -1; var revision = -1L;
            shell.BuyRequested += (selected, version) => { count++; index = selected; revision = version; };
            for (var render = 0; render < 3; render++) shell.Render(capture);
            old.EmitSignal(Button.SignalName.Pressed);
            if (count != 0) return false;
            offers.GetChild<Node>(0).GetNode<Button>("Actions/Buy").EmitSignal(Button.SignalName.Pressed);
            return count == 1 && index == capture.Offers[0].Index && revision == capture.Offers[0].Revision
                && ReferenceEquals(capture, presenter.View);
        }
        finally { owner.RemoveChild(shell); shell.Free(); }
    }

    public static bool RenderedScene(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        scene.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        scene.Size = new Vector2(1280, 720);
        owner.AddChild(scene);
        try
        {
            // Read the coordinator to compare actual controls against the same captured domain state.
            var presenter = (MatchPresenter)typeof(MinimalPlaytest).GetField("_presenter",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(scene)!;
            const string root = "MatchShell";
            const string main = root + "/ContextRow";
            void Press(Button button) => button.EmitSignal(Button.SignalName.Pressed);
            var sawMonster = false; var sawPvp = false; var sawCardEvent = false; var sawModifier = false;
            for (var turn = 0; turn < 40; turn++)
            {
                if (presenter.View.Page == MatchPage.HeroSelection)
                {
                    var candidates = scene.GetNode<HeroSelectionView>(main + "/ContextHost/HeroSelectionView");
                    var heroIndex = presenter.View.Heroes.ToList().FindIndex(hero => hero.Key == new StringName("hero.paladin"));
                    for (var index = 0; index < heroIndex; index++) Press(candidates.GetNode<Button>("Next"));
                    Press(candidates.GetNode<Button>("Choose"));
                }
                var choices = presenter.View.Choices;
                var chosen = choices.FirstOrDefault(choice => !sawCardEvent && choice.Key == new StringName("encounter.landfill"))
                    ?? choices.FirstOrDefault(choice => !sawModifier && choice.Key == new StringName("encounter.training_ground"))
                    ?? choices.First();
                var choiceIndex = choices.ToList().IndexOf(chosen);
                Press(scene.GetNode<HBoxContainer>($"{main}/ContextHost/EncounterChoiceView/Choices").GetChild<Button>(choiceIndex));
                switch (presenter.View.Page)
                {
                    case MatchPage.Shop:
                        var offer = presenter.View.Offers.FirstOrDefault(item => item.Action.Enabled
                            && item.Card.Abilities.Any(ability => ability.Effects.Any(effect => effect is AttributeDamageEffectDefinition)))
                            ?? presenter.View.Offers.FirstOrDefault(item => item.Action.Enabled);
                        if (offer is not null) Press(scene.GetNode<HBoxContainer>($"{main}/ContextHost/ShopView/OfferScroll/Offers").GetChild<Node>(offer.Index).GetNode<Button>("Actions/Buy"));
                        break;
                    case MatchPage.Event:
                        if (presenter.View.EventOptions.Count == 0) break;
                        var options = presenter.View.EventOptions;
                        var selected = options.FirstOrDefault(option => option.Key == new StringName("encounter.landfill.material_small_card"))
                            ?? options.FirstOrDefault(option => option.Key == new StringName("encounter.training_ground.sparring"))
                            ?? options[0];
                        var before = presenter.View.Player!;
                        Press(scene.GetNode<VBoxContainer>($"{main}/ContextHost/EventView/ActionScroll/Actions").GetChild<Button>(options.ToList().IndexOf(selected)));
                        var after = presenter.View.Player!;
                        if (selected.Key == new StringName("encounter.landfill.material_small_card") && after.Cards.Count > before.Cards.Count)
                        {
                            var added = after.Cards.Single(card => !before.Cards.Any(old => old.Id == card.Id));
                            var placement = after.BoardPlacements.Single(item => item.CardId == added.Id);
                            var gridPath = placement.Zone == BoardZone.Battlefield
                                ? $"{root}/BattlefieldRow/Content/Board"
                                : $"{root}/BenchRow/Content/Board";
                            if (!scene.GetNode<Control>(gridPath).GetNode<Button>("Card_" + added.Id.Value.ToString("N")).TooltipText.Contains(added.DisplayName)) return false;
                            var board = scene.GetNode<Control>(gridPath);
                            var cardNode = board.GetNode<Button>("Card_" + added.Id.Value.ToString("N"));
                            var instance = cardNode.GetInstanceId();
                            var capture = presenter.View;
                            foreach (var size in new[] { new Vector2(1280, 720), new Vector2(1920, 1080) })
                            {
                                scene.Size = size;
                                if (board.GetNode<Button>(cardNode.Name.ToString()).GetInstanceId() != instance
                                    || !ReferenceEquals(capture, presenter.View)) return false;
                                var battle = scene.GetNode<Control>($"{root}/BattlefieldRow/Content");
                                var bench = scene.GetNode<Control>($"{root}/BenchRow/Content");
                                if (battle.Position.X != bench.Position.X || battle.Size.X != bench.Size.X) return false;
                                var details = scene.GetNode<CardDetailsView>($"{root}/CardDetails");
                                details.ShowCard(added, size, size);
                                if (details.Position.X < 0 || details.Position.Y < 0
                                    || details.Position.X + details.Size.X > size.X
                                    || details.Position.Y + details.Size.Y > size.Y) return false;
                                details.Hide();
                            }
                            sawCardEvent = true;
                        }
                        if (selected.Key == new StringName("encounter.training_ground.sparring"))
                            foreach (var card in after.Cards)
                            {
                                var old = before.Cards.First(item => item.Id == card.Id);
                                if (card.CurrentValues.TryGetValue(GameAttributeKeys.AttackDamage, out var damage)
                                    && old.CurrentValues.TryGetValue(GameAttributeKeys.AttackDamage, out var previous) && damage > previous)
                                {
                                    var placement = after.BoardPlacements.Single(item => item.CardId == card.Id);
                                    var slot = scene.GetNode<Control>($"{root}/BattlefieldRow/Content/Board")
                                        .GetNode<Button>("Card_" + card.Id.Value.ToString("N"));
                                    if (!slot.TooltipText.Contains($"当前 {damage}")) return false;
                                    sawModifier = true;
                                }
                            }
                        break;
                    case MatchPage.Preparation:
                        if (!scene.GetNode<Control>($"{main}/ContextHost/EnemyBoard").Visible) return false;
                        if (!scene.GetNode<Control>($"{main}/ContextHost/EnemyBoard")
                            .GetChildren().OfType<Button>().Any(button => button.TooltipText.Length > 0)) return false;
                        sawPvp |= presenter.View.Player!.Round > 1 && presenter.View.Player.Turn == 1;
                        sawMonster |= presenter.View.Player!.Turn == 5;
                        Press(scene.GetNode<Button>($"{main}/Leave/LeaveScroll/LeavePanel/Battle"));
                        if (presenter.View.Page != MatchPage.BattlePlayback) return false;
                        var session = (MatchSession)typeof(MatchPresenter).GetField("_player",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(presenter)!;
                        var settled = MatchSnapshot.From(session);
                        var random = session.Random.State;
                        Press(scene.GetNode<Button>($"{main}/Leave/LeaveScroll/LeavePanel/Pause"));
                        presenter.AdvancePlayback(1);
                        if (presenter.View.Playback is not { Paused: true, Tick: 0 }) return false;
                        Press(scene.GetNode<Button>($"{main}/Leave/LeaveScroll/LeavePanel/Speed"));
                        if (presenter.View.Playback.Speed != 2) return false;
                        presenter.StartBattle(); presenter.ContinueMatch(); presenter.ClaimMonsterReward();
                        Press(scene.GetNode<Button>($"{main}/Leave/LeaveScroll/LeavePanel/Pause"));
                        presenter.AdvancePlayback(.1);
                        if (presenter.View.Playback?.Tick != 2) return false;
                        Press(scene.GetNode<Button>($"{main}/Leave/LeaveScroll/LeavePanel/Skip"));
                        presenter.SkipPlayback();
                        var completed = MatchSnapshot.From(session);
                        if (completed.Wealth != settled.Wealth || completed.Experience != settled.Experience
                            || completed.Reputation != settled.Reputation || completed.PvpWins != settled.PvpWins
                            || completed.Round != settled.Round || completed.Turn != settled.Turn
                            || completed.PendingMonsterRewards.Count != settled.PendingMonsterRewards.Count
                            || session.Random.State != random || presenter.View.Playback is not null) return false;
                        break;
                }
                Press(scene.GetNode<Button>($"{main}/Leave/LeaveScroll/LeavePanel/Continue"));
                if (sawMonster && sawPvp && sawCardEvent && sawModifier) break;
            }
            Press(scene.GetNode<Button>($"{main}/Leave/LeaveScroll/LeavePanel/Reset"));
            return sawMonster && sawPvp && sawCardEvent && sawModifier && presenter.View.Player is null
                && !scene.GetNode<Control>($"{main}/ContextHost/EnemyBoard").Visible
                && !scene.GetNode<Control>($"{root}/BattlefieldRow/Content/Board").Visible;
        }
        finally { owner.RemoveChild(scene); scene.Free(); }
    }

    private static DefinitionRegistry Registry(bool mediumShop = false)
    {
        var all = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        // Keep the real card/monster pool; exactly three normal encounters make event checks reproducible.
        return DefinitionRegistry.Create(all.Heroes.Values.Cast<object>().Concat(all.Cards.Values)
            .Concat(all.Skills.Values).Concat(all.Sets.Values).Concat(all.Monsters.Values)
            .Concat(new object[] { mediumShop ? (object)new MediumShopEncounterDefinition() : new SmallShopEncounterDefinition(), new LandfillEncounterDefinition(),
                new TrainingGroundEncounterDefinition(), new PvpEncounterDefinition() }));
    }

    internal static MatchPresenter CreatePresenter(bool mediumShop = false, IOpponentProvider? opponents = null,
        int? encounterLevelOverride = null)
    {
        var registry = Registry(mediumShop);
        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board, registry.Cards.Values);
        var game = new GameCoordinator(new CreateMatchService(factory),
            new EncounterScheduler(registry, allowIncompleteMonsterChoices: true, encounterLevelOverride: encounterLevelOverride),
            new StartBattleService(new BattleSetupFactory(registry.Sets), new CombatSimulator()),
            new MatchResultService(board));
        var presenter = new MatchPresenter(registry, board, economy, new ShopCardPoolService(),
            new ResolveEncounterOptionService(factory, board, registry.Cards.Values),
            new MonsterRewardClaimService(registry, economy, new SkillAcquisitionService(factory), board), game,
            opponents ?? new LocalTestOpponentProvider(registry));
        presenter.Reset();
        presenter.SelectHero(new StringName("hero.paladin"));
        return presenter;
    }

    // 怪物与 PvP 都使用注入来源，页面切换不创建具体适配器或重复请求。
    public static bool OpponentProviderInjection()
    {
        var source = new RecordingOpponentProvider();
        var presenter = CreatePresenter(opponents: source);
        for (var turn = 1; turn <= 8; turn++)
        {
            var choice = presenter.View.Choices.First(item => turn is 4 or 8
                || item.Key.ToString().StartsWith("encounter.shop.", StringComparison.Ordinal));
            presenter.ChooseEncounter(choice.Key);
            if (turn is 4 or 8)
            {
                if (presenter.View.Page != MatchPage.Preparation || presenter.View.Enemy?.Wealth != 777) return false;
                presenter.StartBattle();
                presenter.SkipPlayback();
            }
            presenter.ContinueMatch();
        }
        return source.MonsterRequests == 1 && source.PvpRequests == 1;
    }

    // 用可辨认的对手快照验证来源替换，保持正式定义池不变。
    private sealed class RecordingOpponentProvider : IOpponentProvider
    {
        public int MonsterRequests { get; private set; }
        public int PvpRequests { get; private set; }

        public MatchSession CreateOpponent(ulong seed)
        {
            PvpRequests++;
            return Create(seed);
        }

        public MatchSession CreateMonsterOpponent(ulong seed, MonsterDefinition definition)
        {
            MonsterRequests++;
            return Create(seed);
        }

        private static MatchSession Create(ulong seed) =>
            new CreateMatchService(new EntityFactory()).Create(seed, 772, new PaladinHeroDefinition());
    }

    public static bool SnapshotIsolation()
    {
        var factory = new EntityFactory();
        var session = new CreateMatchService(factory).Create(42, 100, new PaladinHeroDefinition());
        var card = new CardEconomyService(factory).AcquireCard(session, new ArmguardCardDefinition(), 1,
            CardAcquisitionSource.Reward).Value!.Card;
        var before = MatchSnapshot.From(session).Cards.Single();
        card.Attributes.BaseCombat.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.AttackDamage, 7));
        card.Attributes.Persistent.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.Value, 9));
        var after = MatchSnapshot.From(session).Cards.Single();
        var sold = new CardEconomyService(factory).SellCard(session, card.Id);
        return before.CurrentValues[GameAttributeKeys.AttackDamage] == 5
            && after.BaseValues[GameAttributeKeys.AttackDamage] == 5
            && after.CurrentValues[GameAttributeKeys.AttackDamage] == 12
            && after.Value == before.Value + 9 && sold.Value == after.Value
            && CardDisplayAdapter.FaceEffects(after).Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == "12");
    }

    public static bool EffectSemanticsAndArtwork()
    {
        var registry = Registry();
        foreach (var definition in registry.Cards.Values)
            foreach (var level in Enumerable.Range(1, 5).Where(definition.SupportsLevel))
                _ = CardDisplayAdapter.Details(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level)));
        foreach (var definition in registry.Skills.Values)
            foreach (var level in Enumerable.Range(1, 5).Where(definition.SupportsLevel))
                _ = CardDisplayAdapter.SkillDetails(MatchDisplayQuery.FromSkill(definition, level));
        foreach (var threshold in registry.Sets.Values.SelectMany(set => set.Thresholds))
            _ = CardDisplayAdapter.AbilityDetails(threshold.Abilities);
        var nun = MatchDisplayQuery.FromOffer(ShopOffer.Create(new NunCardDefinition()));
        var hammer = MatchDisplayQuery.FromOffer(ShopOffer.Create(new JudgmentHammerCardDefinition()));
        var adapter = new CardDisplayAdapter();
        return CardDisplayAdapter.FaceEffects(nun).Any(effect => effect.Kind == CardFaceEffectKind.Healing && effect.Value == "10")
            && CardDisplayAdapter.FaceEffects(hammer).Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == "20%")
            && CardDisplayAdapter.Details(hammer).Contains("目标最大生命的 20%")
            && adapter.Artwork(nun.Illustration).ResourcePath == nun.Illustration.ToString()
            && adapter.Artwork(hammer.Illustration).ResourcePath == hammer.Illustration.ToString()
            && adapter.Artwork(new StringName("")) == adapter.Artwork(new StringName("res://missing-illustration.png"));
    }

    // 详情按能力用途显示属性；不把辅助卡的占位值当作攻击/冷却。
    public static bool RelevantDetailAttributes()
    {
        foreach (var definition in new CardDefinition[]
            { new CathedralCardDefinition(), new BeastHideCardDefinition(), new DiamondCardDefinition(),
                new JewelryBagCardDefinition(), new TreasureChestCardDefinition() })
        {
            var text = CardDisplayAdapter.Details(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)));
            if (text.Contains("\n攻击：") || text.Contains("\n冷却：")) return false;
        }
        foreach (var definition in new CardDefinition[]
            { new HolyGriffinCardDefinition(), new BlacksmithCardDefinition(), new NunCardDefinition() })
        {
            var text = CardDisplayAdapter.Details(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)));
            if (text.Contains("\n攻击：") || !text.Contains("\n冷却：")) return false;
        }
        var boar = MatchDisplayQuery.FromOffer(ShopOffer.Create(new BoarCardDefinition()));
        var zeroAttack = boar with { CurrentValues = new System.Collections.Generic.Dictionary<StringName, int>
            { [GameAttributeKeys.AttackDamage] = 0, [GameAttributeKeys.CooldownTicks] = 60 } };
        var thorn = MatchDisplayQuery.FromOffer(ShopOffer.Create(new ThornArmorCardDefinition()));
        return CardDisplayAdapter.Details(zeroAttack).Contains("攻击：基础 20 / 当前 0")
            && CardDisplayAdapter.Details(zeroAttack).Contains("冷却：基础 6秒 / 当前 6秒")
            && CardDisplayAdapter.Details(thorn).Contains("攻击：基础 0 / 当前 0")
            && CardDisplayAdapter.Details(thorn).Contains("护甲：基础 10 / 当前 10");
    }

    // 定义中的有序词条贯穿所有等级的实例、报价、对局快照与详情。
    public static bool FormalDescriptions()
    {
        var registry = Registry(); var factory = new EntityFactory();
        var economy = new CardEconomyService(factory);
        var session = new CreateMatchService(factory).Create(42, 100, new PaladinHeroDefinition());
        foreach (var definition in registry.Cards.Values)
        {
            var identity = definition.Attributes.Identity;
            var expected = identity.DescriptionEntries;
            if (expected.Count == 0) return false;
            var text = string.Join("\n", expected.Select(entry => $"{CardKeywords.DisplayName(entry.KeywordKey)}：{entry.Text}"));
            foreach (var level in Enumerable.Range(1, 5).Where(definition.SupportsLevel))
            {
                var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
                if (!offer.DescriptionEntries.SequenceEqual(expected)
                    || !factory.CreateCard(definition, level).Attributes.Identity.DescriptionEntries.SequenceEqual(expected)
                    || !CardDisplayAdapter.Details(offer).Contains(text)) return false;
                var markers = Array.AsReadOnly(new CardDescriptionEntry[]
                {
                    new(CardKeywords.Echo, "当验证事件完成后，显示快照描述；发动：正文不分段。"),
                    new(CardKeywords.Echo, "第二条回响。"),
                });
                if (!CardDisplayAdapter.Details(offer with { DescriptionEntries = markers }).Contains(
                    "回响：当验证事件完成后，显示快照描述；发动：正文不分段。\n回响：第二条回响。")) return false;
                try { ((System.Collections.Generic.IList<CardDescriptionEntry>)offer.DescriptionEntries).Clear(); return false; }
                catch (NotSupportedException) { }
            }
            if (economy.AcquireCard(session, definition, definition.InitialLevel, CardAcquisitionSource.Reward).IsFailure) return false;
        }
        var snapshot = MatchSnapshot.From(session);
        return snapshot.Cards.Count == registry.Cards.Count
            && snapshot.Cards.All(card => card.DescriptionEntries.SequenceEqual(registry.Cards[card.Key].Attributes.Identity.DescriptionEntries));
    }

    // 核对全部正式插画的只读身份、实例、报价与对局快照，以及实际纹理加载。
    public static bool FormalIllustrations()
    {
        if (typeof(CardIdentityAttributes).GetProperty(nameof(CardIdentityAttributes.Illustration))!.CanWrite) return false;
        var registry = Registry();
        var adapter = new CardDisplayAdapter();
        var factory = new EntityFactory();
        var economy = new CardEconomyService(factory);
        var session = new CreateMatchService(factory).Create(42, 100, new PaladinHeroDefinition());
        foreach (var definition in registry.Cards.Values)
        {
            var identity = definition.Attributes.Identity;
            var expected = identity.Illustration;
            if (!expected.IsEmpty && !expected.ToString().StartsWith("res://art/ui/card-face/artwork/")) return false;
            foreach (var level in Enumerable.Range(1, 5).Where(definition.SupportsLevel))
            {
                var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
                var instance = factory.CreateCard(definition, level);
                if (offer.Illustration != expected || instance.Attributes.Identity.Illustration != expected) return false;
                var texture = adapter.Build(offer).Artwork;
                if (texture is null) return false;
                if (expected.IsEmpty)
                {
                    if (texture.GetWidth() != 32 || texture.GetHeight() != 32) return false;
                }
                else if (texture.ResourcePath != expected.ToString()
                    || texture.GetWidth() < 512 || texture.GetHeight() < 512) return false;
            }
            if (economy.AcquireCard(session, definition, definition.InitialLevel, CardAcquisitionSource.Reward).IsFailure) return false;
        }
        var snapshot = MatchSnapshot.From(session);
        return snapshot.Cards.Count == registry.Cards.Count && snapshot.Cards.All(card => card.Illustration == registry.Cards[card.Key].Attributes.Identity.Illustration);
    }

    public static bool RefreshAndStaleOffers()
    {
        var first = CreatePresenter(mediumShop: true);
        var control = CreatePresenter(mediumShop: true);
        first.ChooseEncounter(new StringName("encounter.shop.medium"));
        control.ChooseEncounter(new StringName("encounter.shop.medium"));
        var old = first.View.Offers[0];
        for (var count = 0; count < 5; count++) first.RefreshView();
        if (!first.View.Offers.Select(offer => offer.Card.Key).SequenceEqual(control.View.Offers.Select(offer => offer.Card.Key))) return false;
        first.RefreshShop(); control.RefreshShop();
        if (first.View.Offers[0].Revision == old.Revision) return false;
        var wealth = first.View.Player!.Wealth;
        first.BuyCard(old.Index, old.Revision);
        if (first.View.Player!.Wealth != wealth || first.View.Player.Cards.Count != 0) return false;
        var current = first.View.Offers[0];
        first.BuyCard(current.Index, current.Revision);
        var after = first.View.Player!;
        first.BuyCard(current.Index, current.Revision);
        return first.View.Player!.Wealth == after.Wealth && first.View.Player.Cards.Count == after.Cards.Count
            && first.View.Offers.Select(offer => offer.Card.Key).SequenceEqual(control.View.Offers.Select(offer => offer.Card.Key));
    }

    public static bool EventAcquisitionRefresh()
    {
        var presenter = CreatePresenter();
        presenter.ChooseEncounter(new StringName("encounter.landfill"));
        var revision = presenter.View.EventRevision;
        presenter.ResolveEventOption(new StringName("encounter.landfill.material_small_card"), revision);
        var snapshot = presenter.View.Player!;
        presenter.ResolveEventOption(new StringName("encounter.landfill.material_small_card"), revision);
        return snapshot.Cards.Count == 1 && snapshot.BoardPlacements.Count == 1
            && snapshot.BoardPlacements[0].CardId == snapshot.Cards[0].Id
            && presenter.View.Player!.Cards.Count == 1 && presenter.View.EventOptions.Count == 0
            && presenter.View.Continue.Visible;
    }

    public static bool EventModifierRefresh()
    {
        var presenter = CreatePresenter();
        presenter.ChooseEncounter(new StringName("encounter.shop.small"));
        var offer = presenter.View.Offers.FirstOrDefault(item => item.Card.Abilities
            .Any(ability => ability.Effects.Any(effect => effect is AttributeDamageEffectDefinition)));
        if (offer is null) return false;
        presenter.BuyCard(offer.Index, offer.Revision);
        var before = presenter.View.Player!.Cards.Single();
        presenter.ContinueMatch();
        presenter.ChooseEncounter(new StringName("encounter.training_ground"));
        presenter.ResolveEventOption(new StringName("encounter.training_ground.sparring"), presenter.View.EventRevision);
        var after = presenter.View.Player!.Cards.Single();
        return after.CurrentValues[GameAttributeKeys.AttackDamage] == before.CurrentValues[GameAttributeKeys.AttackDamage] + 5
            && CardDisplayAdapter.FaceEffects(after).Any(effect => effect.Kind == CardFaceEffectKind.Damage
                && effect.Value == after.CurrentValues[GameAttributeKeys.AttackDamage].ToString());
    }

    public static bool EnemyVisibilityAndReset()
    {
        var presenter = CreatePresenter();
        var monsterSeen = false;
        var pvpSeen = false;
        for (var count = 0; count < 8; count++)
        {
            var choice = presenter.View.Player!.EncounterChoices[0];
            presenter.ChooseEncounter(choice.Key);
            if (presenter.View.Page == MatchPage.Shop)
            {
                var offer = presenter.View.Offers.FirstOrDefault(item => item.Action.Enabled);
                if (offer is not null) presenter.BuyCard(offer.Index, offer.Revision);
            }
            else if (presenter.View.Page == MatchPage.Preparation)
            {
                if (!presenter.View.EnemyVisible || presenter.View.Enemy?.BoardPlacements.Count == 0) return false;
                monsterSeen |= choice.Kind == EncounterKind.Monster;
                pvpSeen |= choice.Kind == EncounterKind.Pvp;
                presenter.StartBattle();
                presenter.SkipPlayback();
                var after = presenter.View.Player!;
                presenter.StartBattle();
                if (presenter.View.Player!.Wealth != after.Wealth || presenter.View.Player.PvpWins != after.PvpWins) return false;
            }
            presenter.ContinueMatch();
        }
        presenter.Reset();
        if (presenter.View.Player is not null || presenter.View.Enemy is not null || presenter.View.SelectedCardId is not null
            || presenter.View.EnemyVisible || presenter.View.Offers.Count != 0) return false;
        presenter.SelectHero(new StringName("hero.paladin"));
        return monsterSeen && pvpSeen && presenter.View.Player!.Wealth == 999
            && presenter.View.Player.Cards.Count == 0 && presenter.View.Player.BoardPlacements.Count == 0;
    }
}
