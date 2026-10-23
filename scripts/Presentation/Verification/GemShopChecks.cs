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
using Project_Star.Application.Mentors;
using Project_Star.Content.Cards;
using Project_Star.Content.Encounters;
using Project_Star.Content.Gems;
using Project_Star.Content.Heroes;
using Project_Star.Content.Sets;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 验证宝石正式注册、原发动组合、渠道原子性及真实砧板拖放。
internal static class GemShopChecks
{
    internal static bool ActivationAndPrimary()
    {
        var catalog = DefinitionRegistry.Scan(typeof(GemShopChecks).Assembly);
        Require(catalog.Gems.Count == 2 && catalog.Encounters[new StringName("encounter.shop.gems")] is GemShopDefinition { Level: 4, OfferCount: 3, SocketCost: 20 }, "正式定义");
        foreach (var gem in catalog.Gems.Values)
        foreach (var level in new[] { 3, 4 })
        {
            var (session, card, board) = Statue(level);
            var before = MatchSnapshot.From(session);
            Require(new SocketGemService().Execute(session, new SocketGemCommand(session.Id, card.Id, 0, gem)).IsSuccess, "镶嵌");
            card.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Armor, 999);
            card.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Multicast, 1);
            var snapshot = MatchSnapshot.From(session).Cards.Single();
            Require(before.Cards.Single().GemSockets[0] is null && snapshot.GemSockets[0]?.Key == gem.Attributes.Identity.Key
                && snapshot.Abilities.Count == 2 && snapshot.Abilities[0].Effects.Count == 3
                && CardDisplayAdapter.Details(snapshot).Contains(gem.Attributes.Identity.DisplayName), "投影与冻结");
            var enemy = new CreateMatchService(new EntityFactory()).Create(21, 0, new RobinHeroDefinition());
            enemy.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
            var setup = new BattleSetupFactory().Create(session, enemy, 42, new BattleTick(102), new BattleTick(103));
            var result = new CombatSimulator().Simulate(setup);
            var status = ((ApplyPrimaryEffectPercentStatusEffectDefinition)gem.ActiveEffects.Single()).Status;
            Require(result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == status).Select(item => item.Amount)
                .SequenceEqual(new[] { level == 3 ? 10 : 20, level == 3 ? 10 : 20 }), "主值与多重");
            Require(result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == card.Id) == 4
                && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "发动数量与确定性");
            var frozen = setup.Player.Cards.Single().Abilities!;
            if (level == 3)
            {
                var upgraded = new CardEconomyService(new EntityFactory(), board).AcquireCard(session, new RuinStoneStatueCardDefinition(), 3, CardAcquisitionSource.Reward);
                Require(upgraded.IsSuccess && upgraded.Value!.Card.Id == card.Id && card.GemSockets[0] == gem
                    && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 4
                    && setup.Player.Cards.Single().AttackDamage == 100 && frozen[0].Effects.Count == 3, "升级保留与旧输入");
                var next = new CombatSimulator().Simulate(new BattleSetupFactory().Create(session, enemy, 42, new BattleTick(102), new BattleTick(103)));
                Require(next.Events.OfType<StatusChangedEvent>().Where(item => item.Status == status).All(item => item.Amount == 20), "升级后主值");
            }
        }
        foreach (var primary in new EffectDefinition[] { new HealEffectDefinition(107), new GainSourceHeroArmorEffectDefinition(107),
            new ApplyStatusEffectDefinition(BattleStatus.Burn, 107), new ApplyStatusEffectDefinition(BattleStatus.Poison, 107),
            new ApplyStatusEffectDefinition(BattleStatus.HealthRegen, 107), new ApplyStatusEffectDefinition(BattleStatus.ManaRegen, 107),
            new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.AttackDamage),
            new ApplySourceAttributePercentStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.AttackDamage, 100) })
        {
            var ability = new AbilityDefinition(new StringName("verification.gem.primary"), AbilityActivation.Active,
                primary is ApplySourceAttributePercentStatusEffectDefinition ? AbilityTarget.EnemyHero : AbilityTarget.AlliedHero, 0, 1,
                [primary, new GainSourceHeroArmorEffectDefinition(999), new ApplyPrimaryEffectPercentStatusEffectDefinition(BattleStatus.Poison, 10)]);
            var card = new CardBattleSetup(EntityId.New(), 0, 107, 1, [ability], UseLegacyAttack: false)
            { CombatValues = new System.Collections.Generic.Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 107 } };
            var result = new CombatSimulator().Simulate(new BattleSetup(
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [card]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), []), 42, new BattleTick(10), new BattleTick(11)));
            var frame = result.States.Last(frame => frame.Tick.Value == 10 && frame.Opponent.Health > 0);
            var expected = primary is ApplyStatusEffectDefinition or ApplyAttributeStatusEffectDefinition or ApplySourceAttributePercentStatusEffectDefinition ? 107 : 10;
            var preview = MatchSnapshot.From(Statue(3).Session).Cards.Single() with
            {
                Abilities = [ability], CurrentValues = card.CombatValues!, EffectAmounts = Array.Empty<int?>(),
            };
            Require(CardDisplayAdapter.FaceEffects(preview).Contains(new CardFaceEffect(CardFaceEffectKind.Poison, expected.ToString())),
                "独立预览使用相同属性价值换算");
            if (primary is ApplySourceAttributePercentStatusEffectDefinition) expected += 107;
            var ownPoison = primary is ApplyStatusEffectDefinition { Status: BattleStatus.Poison } ? 107 : 0;
            Require(frame.Opponent.Poison == expected && frame.Player.Poison == ownPoison,
                $"属性价值换算、敌方目标与取整：{primary}，敌方{frame.Opponent.Poison}/{expected}，己方{frame.Player.Poison}/{ownPoison}");
        }
        var boosted = new CardBattleSetup(EntityId.New(), 0, 107, 1,
            [new AbilityDefinition(new StringName("verification.gem.berserk"), AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
                [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)]),
             new AbilityDefinition(new StringName("verification.gem.boosted"), AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 1,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage), new GainSourceHeroArmorEffectDefinition(999),
                    new ApplyPrimaryEffectPercentStatusEffectDefinition(BattleStatus.Burn, 10), new ApplyPrimaryEffectPercentStatusEffectDefinition(BattleStatus.Poison, 10)])],
            UseLegacyAttack: false, Multicast: 1)
        { CombatValues = new System.Collections.Generic.Dictionary<StringName, int> { [GameAttributeKeys.BurnBonus] = 3 } };
        var boostedResult = new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), [boosted]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), []), 42, new BattleTick(12), new BattleTick(13)));
        Require(boostedResult.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Burn).Select(item => item.Amount).SequenceEqual(new[] { 15, 15 })
            && boostedResult.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Poison).Select(item => item.Amount).SequenceEqual(new[] { 12, 12 }), "灼伤加成和狂暴各结算一次");
        return true;
    }

    internal static bool FeesAndRefresh()
    {
        var catalog = DefinitionRegistry.Scan(typeof(GemShopChecks).Assembly);
        var service = new GemShopService(catalog); var (session, card, board) = Statue(3);
        var shop = new GemShopEncounterDefinition();
        Require(service.Open(session, shop).IsSuccess, "开店");
        var visit = session.ActiveGemShopVisit!;
        Require(visit.Offers.Count == 2 && visit.Offers.Select(gem => gem.Attributes.Identity.Key).Distinct().Count() == 2, "两个砧板无重复");
        var command = new SocketShopGemCommand(session.Id, card.Id, 0, visit.Revision);
        var wealth = session.Player.Wealth; var random = session.Random.State;
        Require(service.CheckSocket(session, command).IsSuccess && session.Player.Wealth == wealth && session.Random.State == random, "预览只读");
        Require(service.Socket(session, command with { MatchId = Guid.NewGuid() }).IsFailure
            && service.Socket(session, command with { OfferIndex = 2 }).IsFailure
            && service.Socket(session, command with { CardId = EntityId.New() }).IsFailure
            && session.Player.Wealth == wealth && session.Random.State == random, "过期与无效输入无副作用");
        Require(service.Refresh(session).IsSuccess && session.Player.Wealth == wealth - 8 && visit.HasRefreshed
            && service.Socket(session, command).IsFailure && service.Refresh(session).IsFailure, "刷新价格次数与旧砧板");
        command = command with { Revision = visit.Revision };
        Require(service.Socket(session, command).IsSuccess && session.Player.Wealth == wealth - 28 && visit.IsResolved
            && service.Socket(session, command with { Revision = visit.Revision, OfferIndex = 1 }).IsFailure
            && service.Refresh(session).IsFailure, "只能镶嵌一次");
        var previous = visit.Revision; service.Close(session); service.Open(session, shop);
        Require(session.ActiveGemShopVisit!.Revision > previous && service.Socket(session, command).IsFailure, "跨访问隔离");
        var passive = new EntityFactory().CreateCard(new AdventurerBackpackCardDefinition()); session.Player.Inventory.Add(passive);
        Require(board.PlaceCard(session, passive.Id, BoardZone.Bench, 0).IsSuccess, "备战放置");
        command = command with { CardId = passive.Id, Revision = session.ActiveGemShopVisit.Revision };
        Require(service.Socket(session, command).IsFailure && session.Player.Wealth == wealth - 28, "被动卡不可镶嵌");
        var (poor, target, _) = Statue(3); poor.Player.TrySpendWealth(poor.Player.Wealth - 7); service.Open(poor, shop);
        random = poor.Random.State;
        Require(service.Refresh(poor).IsFailure && service.Socket(poor, new SocketShopGemCommand(poor.Id, target.Id, 0, poor.ActiveGemShopVisit!.Revision)).IsFailure
            && poor.Player.Wealth == 7 && poor.Random.State == random && target.GemSockets[0] is null, "不足金币");
        poor.Status = MatchStatus.Won;
        Require(service.Open(poor, shop).IsFailure && service.Refresh(poor).IsFailure, "结束对局禁止渠道");
        return true;
    }

    internal static bool DragScene(Control owner)
    {
        var (presenter, economy) = CreatePresenter();
        var shell = CreateShell(owner);
        try
        {
            shell.GemSocketPreviewRequested += (drag, index, revision) => presenter.PreviewGemSocket(drag.MatchId, drag.CardId, index, revision);
            shell.GemSocketRequested += (drag, index, revision) => presenter.SocketShopGem(drag.MatchId, drag.CardId, index, revision);
            presenter.ViewChanged += shell.Render; presenter.Reset(); presenter.SelectHero(new StringName("hero.robin"));
            var session = OpenGemRound(presenter);
            Require(presenter.View.Choices.Single().Level == 4, "排程等级");
            var card = economy.AcquireAndPlace(session, new RuinStoneStatueCardDefinition(), 3, CardAcquisitionSource.Reward).Value!.Card;
            presenter.ChooseEncounter(new StringName("encounter.shop.gems"));
            var view = presenter.View; var random = session.Random.State; presenter.RefreshView();
            Require(view.Page == MatchPage.GemShop && view.ShopLevel == 4 && view.GemOffers.Count == 2 && view.Refresh.Text.Contains("8 金币")
                && view.Continue.Text == "离开商店" && presenter.View.GemOffers.SequenceEqual(view.GemOffers) && session.Random.State == random, "界面快照无随机副作用");
            var anvils = shell.GetNode<HBoxContainer>("ContextRow/ContextHost/GemShopView/Anvils");
            Require(anvils.GetChildCount() == 2 && shell.GetNode<GemShopView>("ContextRow/ContextHost/GemShopView").Visible, "真实砧板");
            var first = anvils.GetChild<GemAnvilDropZone>(0); var wealth = session.Player.Wealth;
            using var drag = new BoardDragData(session.Id, card.Id, 0, 3);
            Require(first._CanDropData(Vector2.Zero, Variant.From(drag)) && session.Player.Wealth == wealth && card.GemSockets[0] is null, "拖放预览");
            first._DropData(Vector2.Zero, Variant.From(drag));
            Require(session.Player.Wealth == wealth - 20 && card.GemSockets[0] is not null && presenter.View.GemOffers.All(offer => offer.Resolved)
                && !anvils.GetChild<GemAnvilDropZone>(1)._CanDropData(Vector2.Zero, Variant.From(drag)) && !presenter.View.Refresh.Enabled, "真实拖放提交");
            presenter.ContinueMatch(); Require(session.ActiveGemShopVisit is null && presenter.View.Page == MatchPage.EncounterChoice, "离店关闭渠道");
            return true;
        }
        finally { owner.RemoveChild(shell); shell.Free(); }
    }

    // 使用原生GUI命中，覆盖出售遮罩出现后的砧板，而非直接调用Drop方法。
    internal static async Task<bool> NativeDrag(Control owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1600, 900), Disable3D = true, HandleInputLocally = true };
        var capture = OS.GetCmdlineUserArgs().Contains("--capture-gem-drag");
        if (capture) viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        owner.AddChild(viewport);
        var host = new Control(); viewport.AddChild(host); host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var shell = CreateShell(host); shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        async Task Send(InputEvent input)
        {
            if (input is InputEventKey) { Input.ParseInputEvent(input); Input.FlushBufferedEvents(); }
            viewport.PushInput(input, true);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        InputEventMouseMotion Motion(Vector2 point, Vector2 relative, bool down) => new()
            { Position = point, GlobalPosition = point, Relative = relative, ButtonMask = down ? MouseButtonMask.Left : 0 };
        InputEventMouseButton Button(Vector2 point, bool down) => new()
            { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = down, ButtonMask = down ? MouseButtonMask.Left : 0 };
        try
        {
            var (presenter, economy) = CreatePresenter();
            shell.GemSocketPreviewRequested += (drag, index, revision) => presenter.PreviewGemSocket(drag.MatchId, drag.CardId, index, revision);
            shell.GemSocketRequested += (drag, index, revision) => presenter.SocketShopGem(drag.MatchId, drag.CardId, index, revision);
            shell.SellPreviewRequested += drag => presenter.PreviewSale(drag.MatchId, drag.CardId);
            shell.DragSellRequested += presenter.SellDraggedCard;
            shell.CancelRequested += presenter.CancelSelection;
            presenter.ViewChanged += shell.Render; presenter.Reset(); presenter.SelectHero(new StringName("hero.robin"));
            var session = OpenGemRound(presenter);
            var field = economy.AcquireAndPlace(session, new RuinStoneStatueCardDefinition(), 3, CardAcquisitionSource.Reward).Value!.Card;
            var bench = economy.AcquireAndPlace(session, new RuinStoneStatueCardDefinition(), 4, CardAcquisitionSource.Reward).Value!.Card;
            new BoardService(new BoardPlacementSolver()).PlaceCard(session, bench.Id, BoardZone.Bench, 0);
            presenter.ChooseEncounter(new StringName("encounter.shop.gems")); viewport.NotifyMouseEntered();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            var sale = shell.GetNode<SellDropZone>("SellDropZone");
            var wealth = session.Player.Wealth;
            var placements = presenter.View.Player!.BoardPlacements.ToArray();
            foreach (var (card, zone, index) in new[] { (field, "BattlefieldRow", 0), (bench, "BenchRow", 1) })
            {
                shell.SetBenchOpen(zone == "BenchRow");
                var item = shell.GetNode<CardItemView>($"{zone}/Content/Board/Card_{card.Id.Value:N}");
                var press = item.GetGlobalRect().GetCenter();
                var anvils = shell.GetNode<HBoxContainer>("ContextRow/ContextHost/GemShopView/Anvils");
                var target = anvils.GetChild<GemAnvilDropZone>(index).GetGlobalRect().GetCenter();
                var gemKey = presenter.View.GemOffers[index].Key;
                await Send(Motion(press, Vector2.Zero, false)); await Send(Button(press, true));
                await Send(Motion(press + new Vector2(0, 15), new Vector2(0, 15), true));
                shell.SetBenchOpen(false);
                await Send(Motion(target, target - press, true));
                Require(viewport.GuiIsDragging() && sale.Visible && sale._CanDropData(sale.GetGlobalTransform().AffineInverse() * target, viewport.GuiGetDragData()), "拖拽路由优先镶嵌");
                Require(session.Board.Contains(card.Id) && session.Player.Wealth == wealth && card.GemSockets[0] is null, "原生预览不出售或扣费");
                if (capture && card == bench)
                {
                    await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var image = viewport.GetTexture().GetImage();
                    Require(image.SavePng("res://output/gem-shop/drag-separate.png") == Error.Ok, "拖拽分区截图");
                }
                if (card == field)
                {
                    await Send(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
                    await Send(Button(target, false));
                    await Send(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
                    Require(!viewport.GuiIsDragging() && !sale.Visible && card.GemSockets[0] is null && session.Player.Wealth == wealth
                        && placements.SequenceEqual(presenter.View.Player!.BoardPlacements), "取消镶嵌无副作用");
                }
                else
                {
                    await Send(Button(target, false));
                    Require(!viewport.GuiIsDragging() && !sale.Visible && session.Player.Wealth == wealth - 20
                        && card.GemSockets[0]?.Attributes.Identity.Key == gemKey && session.Board.Contains(card.Id)
                        && placements.SequenceEqual(presenter.View.Player!.BoardPlacements), "原生松开仅镶嵌且保留卡牌");
                }
            }
            var source = shell.GetNode<CardItemView>($"BattlefieldRow/Content/Board/Card_{field.Id.Value:N}");
            var start = source.GetGlobalRect().GetCenter(); var sellTarget = sale.GlobalPosition + new Vector2(40, 300);
            var value = field.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            await Send(Motion(start, Vector2.Zero, false)); await Send(Button(start, true));
            await Send(Motion(sellTarget, sellTarget - start, true));
            Require(sale.Visible && viewport.GuiIsDragging(), "独立出售区出现");
            await Send(Motion(sellTarget + Vector2.Right, Vector2.Right, true));
            await Send(Button(sellTarget, false));
            Require(!session.Board.Contains(field.Id) && session.Board.Contains(bench.Id) && session.Player.Wealth == wealth - 20 + value
                && bench.GemSockets[0] is not null && !sale.Visible,
                $"原生出售仅作用于出售卡牌 field={session.Board.Contains(field.Id)} bench={session.Board.Contains(bench.Id)} wealth={session.Player.Wealth}/{wealth - 20 + value} sale={sale.Visible}");
            return true;
        }
        finally
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
            Input.FlushBufferedEvents(); owner.RemoveChild(viewport); viewport.Free();
        }
    }

    private static (MatchPresenter Presenter, CardEconomyService Economy) CreatePresenter()
    {
        var catalog = DefinitionRegistry.Create([new RobinHeroDefinition(), new RuinStoneStatueCardDefinition(), new AdventurerBackpackCardDefinition(),
            new GemShopEncounterDefinition(), new OrangeAugiteGemDefinition(), new EmeraldGemDefinition(), new PvpEncounterDefinition(), new AdventurerSetDefinition()]);
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver()); var economy = new CardEconomyService(factory, board);
        var presenter = new MatchPresenter(catalog, board, economy, new ShopCardPoolService(), new ResolveEncounterOptionService(factory, board, catalog.Cards.Values, catalog.Monsters.Values),
            new MentorService(catalog, new SkillAcquisitionService(factory)), new MonsterRewardClaimService(catalog, economy, new SkillAcquisitionService(factory), board),
            new GameCoordinator(new CreateMatchService(factory), new EncounterScheduler(catalog, allowIncompleteMonsterChoices: true),
                new StartBattleService(new BattleSetupFactory(), new CombatSimulator()), new MatchResultService(board)), new LocalTestOpponentProvider(catalog));
        return (presenter, economy);
    }

    private static MatchShell CreateShell(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Main.tscn").Instantiate();
        var shell = scene.GetNode<MatchShell>("MatchShell"); scene.RemoveChild(shell); scene.Free(); owner.AddChild(shell); return shell;
    }
    private static MatchSession Session(MatchPresenter presenter) => (MatchSession)typeof(MatchPresenter)
        .GetField("_player", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(presenter)!;

    private static MatchSession OpenGemRound(MatchPresenter presenter)
    {
        var session = Session(presenter);
        // 此夹具直接跳到第8轮验证20金币镶嵌，不依赖正式新局的首轮收入。
        session.Player.AddWealth(100);
        session.Progress.Round = 8;
        var catalog = DefinitionRegistry.Create([new GemShopEncounterDefinition()]);
        Require(new EncounterScheduler(catalog).Generate(session).IsSuccess, "第8轮宝石商店解锁");
        presenter.RefreshView();
        return session;
    }

    internal static async Task Capture(Control owner)
    {
        var (presenter, economy) = CreatePresenter(); var shell = CreateShell(owner);
        try
        {
            shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            presenter.Reset(); presenter.SelectHero(new StringName("hero.robin"));
            economy.AcquireAndPlace(OpenGemRound(presenter), new RuinStoneStatueCardDefinition(), 3, CardAcquisitionSource.Reward);
            presenter.ChooseEncounter(new StringName("encounter.shop.gems"));
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                owner.GetWindow().Size = size; owner.GetTree().Root.ContentScaleSize = new Vector2I(1600, 900); shell.Render(presenter.View);
                Input.WarpMouse(Vector2.One);
                owner.GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.One }, true);
                for (var frame = 0; frame < 8; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                var shop = shell.GetNode<GemShopView>("ContextRow/ContextHost/GemShopView");
                var image = owner.GetViewport().GetTexture().GetImage();
                Require(image.SavePng(ProjectSettings.GlobalizePath($"res://output/gem-shop/shop-{size.X}.png")) == Error.Ok, "截图保存");
                Require(shell.GetNode<Control>("ContextRow/ContextHost").GetGlobalRect().Grow(.1f).Encloses(shop.GetGlobalRect()),
                    $"商店边界 host={shell.GetNode<Control>("ContextRow/ContextHost").GetGlobalRect()} shop={shop.GetGlobalRect()}");
                foreach (var anvil in shop.GetNode<HBoxContainer>("Anvils").GetChildren().Cast<GemAnvilDropZone>())
                    Require(shop.GetGlobalRect().Encloses(anvil.GetGlobalRect()), "砧板边界");
            }
        }
        finally { owner.RemoveChild(shell); shell.Free(); }
    }
    private static (MatchSession Session, CardInstance Card, BoardService Board) Statue(int level)
    {
        var factory = new EntityFactory(); var session = new CreateMatchService(factory).Create(42, 100, new RobinHeroDefinition());
        var card = factory.CreateCard(new RuinStoneStatueCardDefinition(), level); session.Player.Inventory.Add(card);
        var board = new BoardService(new BoardPlacementSolver());
        Require(board.PlaceCard(session, card.Id, BoardZone.Battlefield, 0).IsSuccess, "石像放置"); return (session, card, board);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("GemShop: " + message); }
}
