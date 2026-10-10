using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 真实卡牌获取入口、单一详情和正式任务投影的表现层验证。
internal static class CardInteractionChecks
{
    internal static bool HoverRestoration(Control owner)
    {
        var item = new CardItemView { Size = new Vector2(300, 360) };
        owner.AddChild(item);
        try
        {
            item.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new OrderCrusaderCardDefinition())), new CardDisplayAdapter());
            var face = item.GetChildren().OfType<Project_Star.Presentation.CardFace.CardFace>().Single();
            var origin = face.Position;
            item.EmitSignal(Control.SignalName.MouseEntered); Step();
            if (!face.Position.IsEqualApprox(origin + new Vector2(0, -6))) return false;
            item.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new OrderCrusaderCardDefinition())), new CardDisplayAdapter());
            item.EmitSignal(Control.SignalName.MouseExited); Step();
            if (!face.Position.IsEqualApprox(origin)) return false;
            item.EmitSignal(Control.SignalName.MouseEntered); Step();
            item.Size = new Vector2(360, 420);
            item.EmitSignal(Control.SignalName.MouseExited); Step();
            return face.Position.IsEqualApprox((item.Size - face.Size) / 2);
        }
        finally { owner.RemoveChild(item); item.Free(); }
        void Step() => ((Tween)typeof(CardItemView).GetField("_hoverTween", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(item)!).CustomStep(.3);
    }

    // 淡入位移与最终锚点分离；刷新、固定、关闭和重新打开不能累计偏移。
    internal static bool DetailsReveal(Control owner)
    {
        var details = new CardDetailsView(); owner.AddChild(details);
        try
        {
            var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(new BoarCardDefinition(), 2)) with { Id = EntityId.New() };
            var bounds = new Vector2(1600, 900); var target = new Vector2(80, 80);
            details.ShowCard(card, target, bounds);
            var begin = details.Position;
            if (!Mathf.IsEqualApprox(begin.Y, 80 + 6 * details.Scale.Y) || details.Modulate.A != 0) return false;
            details.RefreshCard(card with { Value = 10 }, bounds);
            if (details.Position != begin || details.CurrentCardId != card.Id) return false;
            Step(.06);
            if (details.Position.Y <= 80 || details.Position.Y >= begin.Y || details.Modulate.A <= 0 || details.Modulate.A >= 1) return false;
            if (Mathf.Abs(details.Modulate.A - .8024034f) > .0001f
                || Mathf.Abs(details.Position.Y - (80 + 6 * (1 - .8024034f) * details.Scale.Y)) > .001f) return false;
            Step(.12); details._Process(0);
            var end = details.Position;
            if (!Mathf.IsEqualApprox(end.Y, 80) || !Mathf.IsEqualApprox(details.Modulate.A, 1)) return false;
            details.SetPinned(true); details.RefreshCard(card, bounds);
            if (details.Position != end || details.MouseFilter != Control.MouseFilterEnum.Stop) return false;
            var frame = details.GetNode<LevelBorderView>("LevelBorder"); frame._Process(0);
            if (frame.GetGlobalRect() != details.GetGlobalRect()) return false;
            details.GetNode<Button>("Close").EmitSignal(Button.SignalName.Pressed);
            if (details.Visible || details.CurrentCardId is not null) return false;
            details.ShowCard(card, target, bounds); Step(.2); details._Process(0);
            return details.Position == end && details.CurrentCardId == card.Id;
        }
        finally { owner.RemoveChild(details); details.Free(); }
        void Step(double delta) => ((Tween)typeof(CardDetailsView).GetField("_fade", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(details)!).CustomStep(delta);
    }

    internal static bool DirectAcquisition(Control owner)
    {
        var scene = Create(owner);
        try
        {
            var presenter = Presenter(scene); presenter.SelectHero("hero.paladin");
            presenter.ChooseEncounter(presenter.View.Choices.First(choice => choice.ShopLevel > 0).Key);
            var shell = scene.GetNode<MatchShell>("MatchShell");
            var offer = presenter.View.Offers.First(value => value.Action.Enabled);
            CardItemView Item() => shell.GetNode<CardItemView>($"ContextRow/ContextHost/ShopView/OfferScroll/Offers/Offer{offer.Index}/Card");
            var item = Item(); var wealth = presenter.View.Player!.Wealth;
            presenter.CancelSelection();
            if (!ReferenceEquals(item, Item()) || presenter.View.Player.Wealth != wealth) return false;
            item._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });
            var details = shell.GetNode<CardDetailsView>("CardDetails");
            if (!details.Visible || details.CurrentCardKey != offer.Card.Key || presenter.View.Player.Wealth != wealth
                || item.TooltipText.Length != 0 || item._GetTooltip(Vector2.Zero).Length != 0) return false;
            presenter.RefreshView();
            if (!details.Visible || details.CurrentCardKey != offer.Card.Key) return false;
            item.EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Player.Wealth != wealth) return false;
            item = Item(); item.EmitSignal(Button.SignalName.Pressed); item.EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Player!.Wealth != wealth - offer.Price || !presenter.View.Player.Cards.Any(card => card.Key == offer.Card.Key)) return false;
            Item().EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Player.Wealth != wealth - offer.Price) return false;
            var disabled = presenter.View with { Offers = Array.AsReadOnly(presenter.View.Offers.Select(value => value with
                { Action = value.Action with { Enabled = false, Reason = "余额不足" } }).ToArray()) };
            shell.Render(disabled); Item().EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Player.Wealth != wealth - offer.Price) return false;
            presenter.RefreshView();
            var session = Session(presenter);
            session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card, "card.boar", "野猪", 1));
            presenter.RefreshView();
            var rewards = shell.GetNode<HeroDetailsView>("HeroDetails"); rewards.ShowSection(HeroSection.Rewards, shell.Size);
            var reward = rewards.GetNode<CardItemView>("Content/Scroll/Items/RewardCard0");
            var count = presenter.View.Rewards.Count;
            reward._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });
            if (presenter.View.Rewards.Count != count || details.CurrentCardKey != new StringName("card.boar")) return false;
            var previousWealth = presenter.View.Player!.Wealth;
            reward.EmitSignal(Button.SignalName.Pressed); reward.EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Rewards.Count != 0 || presenter.View.Player!.Wealth != previousWealth
                || !presenter.View.Player.Cards.Any(card => card.Key == new StringName("card.boar"))) return false;
            var sample = MatchDisplayQuery.FromOffer(ShopOffer.Create(new AscendantCardDefinition()));
            details.SetPinned(false); details.ShowNear(sample, new Rect2(100, 100, 60, 100), shell.Size);
            details.RefreshCard(sample, shell.Size);
            if (details.GetNode<Control>("Content").MouseFilter != Control.MouseFilterEnum.Ignore
                || details.GetNode<Control>("Content/CardContent/BodyScroll/Paragraphs/Quests/quest_ascendant_attack").MouseFilter != Control.MouseFilterEnum.Ignore) return false;
            details.SetPinned(true);
            if (details.GetNode<Control>("Content/CardContent/BodyScroll").MouseFilter != Control.MouseFilterEnum.Stop) return false;
            details.GetNode<Button>("Close").EmitSignal(Button.SignalName.Pressed);
            return !details.Visible;
        }
        finally { owner.RemoveChild(scene); scene.Free(); }
    }

    internal static bool QuestProjection(Control owner)
    {
        var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(new AscendantCardDefinition()));
        if (card.QuestDefinitions.Count != 3 || CardQuestViewModel.From(card, null).Any(quest => quest.Progress != 0 || quest.Unlocked)) return false;
        var progress = card.QuestDefinitions.Reverse().Select(quest => new QuestProgressSnapshot(quest.Key, Math.Min(75, quest.RequiredCount), quest.RequiredCount, quest.RequiredCount <= 75)).ToArray();
        card = card with { Quests = Array.AsReadOnly(progress) };
        var projected = CardQuestViewModel.From(card, null);
        if (projected[0].Progress != 20 || projected[1].Progress != 60 || projected[2].Progress != 75
            || !projected[0].Reward.Contains("+80") || !projected[1].Reward.Contains("光属性")
            || !projected[2].Reward.Contains("10%灼伤") || !projected[2].Reward.Contains("神明")) return false;
        var replay = new CardBattleSnapshot(card.Id, SideId.Player, false, 0, 0, 0, [], card.CurrentValues)
            { Quests = Array.AsReadOnly(card.QuestDefinitions.Select(quest => new CardQuestBattleSnapshot(quest.Key, 0, quest.RequiredCount, false)).ToArray()) };
        if (CardQuestViewModel.From(card, replay).Any(quest => quest.Progress != 0 || quest.Unlocked)) return false;
        var cauldron = MatchDisplayQuery.FromOffer(ShopOffer.Create(new MagicCauldronCardDefinition(), 4));
        var staff = MatchDisplayQuery.FromOffer(ShopOffer.Create(new LogStaffCardDefinition(), 4));
        if (!CardQuestViewModel.From(cauldron, null).Single().Condition.Contains("植物")
            || !CardQuestViewModel.From(cauldron, null).Single().Reward.Contains("-2秒")
            || !CardQuestViewModel.From(staff, null)[0].Reward.Contains("32")
            || !CardQuestViewModel.From(staff, null)[2].Reward.Contains("-3秒")) return false;
        var factory = new EntityFactory(); var session = new CreateMatchService(factory).Create(42, 100, new PaladinHeroDefinition());
        var instance = new CardEconomyService(factory).AcquireCard(session, new AscendantCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        var frozen = MatchSnapshot.From(session).Cards.Single();
        instance.SetQuestProgress(instance.Quests[0].Key, 20);
        if (CardQuestViewModel.From(frozen, null)[0].Unlocked || !CardQuestViewModel.From(MatchSnapshot.From(session).Cards.Single(), null)[0].Unlocked) return false;
        var view = new CardDetailsContent(); owner.AddChild(view);
        try
        {
            view.Render(card, 338, 300);
            var paragraphs = view.GetNode<VBoxContainer>("BodyScroll/Paragraphs");
            if (paragraphs.GetChildren().OfType<RichTextLabel>().Any(label => label.GetParsedText().Contains("任务"))
                || view.GetNode<RichTextLabel>("InstanceDetails").GetParsedText().Contains("任务")) return false;
            var row = paragraphs.GetNode<PanelContainer>("Quests/quest_ascendant_divinity");
            if (row.GetNode<Label>("Column/Meter/Count").Text != "75 / 120"
                || row.GetNode<ProgressBar>("Column/Meter/Progress").Value != 75) return false;
            view.Render(card with { Quests = Array.AsReadOnly(progress.Select(quest => quest with { Progress = 140, Unlocked = true }).ToArray()) }, 338, 300);
            row = paragraphs.GetNode<PanelContainer>("Quests/quest_ascendant_divinity");
            if (row.GetNode<ProgressBar>("Column/Meter/Progress").Value != 120 || row.GetNode<Label>("Column/Heading/State").Text != "✓ 已解锁") return false;
            view.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new BoarCardDefinition())), 338, 300);
            return !paragraphs.HasNode("Quests");
        }
        finally { owner.RemoveChild(view); view.Free(); }
    }

    // 原生鼠标和键盘验证后捕获获批任务排版，展示数据仅用于视觉夹具。
    internal static async Task Capture(Control owner)
    {
        var scene = Create(owner);
        owner.GetWindow().MinSize = Vector2I.Zero;
        try
        {
            var presenter = Presenter(scene);
            var shell = scene.GetNode<MatchShell>("MatchShell");
            var details = shell.GetNode<CardDetailsView>("CardDetails");
            await Frames();
            owner.GetViewport().NotifyMouseEntered();
            var roster = shell.GetNode<Button>("ContextRow/ContextHost/HeroSelectionView/Roster/Entries/Hero0");
            await Feedback(roster, "hero-roster");
            presenter.SelectHero("hero.paladin"); await Frames();
            var encounter = shell.GetNode<EncounterSelectionView>("ContextRow/ContextHost/EncounterChoiceView")
                .FindChildren("*", "Button", true, false).OfType<Button>().First(button => button.IsVisibleInTree() && !button.Disabled);
            await Feedback(encounter, "encounter");
            presenter.ChooseEncounter(presenter.View.Choices.First(choice => choice.ShopLevel > 0).Key); await Frames();
            var menu = shell.GetNode<Button>("OpenGameMenu");
            await Feedback(menu, "menu");
            var primary = shell.GetNode<VBoxContainer>("FooterActions").GetChildren().OfType<Button>().First(button => button.IsVisibleInTree() && !button.Disabled);
            await Feedback(primary, "primary");
            shell.SetDesktopPalette(true); await Frames();
            await Feedback(menu, "menu-blue"); await Feedback(primary, "primary-blue");
            shell.SetDesktopPalette(false); await Frames();
            menu.GrabFocus(); KeyDown(Key.Space); await Frames(); await Save("feedback-keyboard-down");
            if (menu.GetDrawMode() is not BaseButton.DrawMode.Pressed and not BaseButton.DrawMode.HoverPressed)
                throw new InvalidOperationException("Space按住时按钮缺少按下状态。");
            KeyUp(Key.Space); await Frames();
            if (!shell.GetNode<MatchMenuView>("GameMenu").Visible) throw new InvalidOperationException("Space释放没有打开菜单。");
            KeyInput(Key.Escape); await Frames();
            if (shell.GetNode<MatchMenuView>("GameMenu").Visible) throw new InvalidOperationException("Escape未关闭菜单。");
            menu.EmitSignal(Button.SignalName.Pressed);
            shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenDisplaySettings").EmitSignal(Button.SignalName.Pressed); await Frames();
            var settings = shell.GetNode<DisplaySettingsView>("DisplaySettings");
            await Feedback(settings.FindChildren("Apply", "Button", true, false).OfType<Button>().Single(), "settings");
            KeyInput(Key.Escape); await Frames();
            if (settings.Visible) throw new InvalidOperationException("设置反馈检查后Escape未关闭。");
            menu.EmitSignal(Button.SignalName.Pressed);
            shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenSkillCatalog").EmitSignal(Button.SignalName.Pressed); await Frames();
            var skills = shell.GetNode<SkillCatalogView>("SkillCatalog");
            await Feedback(skills.FindChildren("*", "Button", true, false).OfType<SkillItemView>()
                .First(button => button.IsVisibleInTree() && !button.Disabled && button.MouseFilter != Control.MouseFilterEnum.Ignore), "skill-catalog");
            KeyInput(Key.Escape); await Frames();
            if (skills.Visible) throw new InvalidOperationException("技能反馈检查后Escape未关闭。");
            var offer = presenter.View.Offers.First(value => value.Action.Enabled);
            var item = shell.GetNode<CardItemView>($"ContextRow/ContextHost/ShopView/OfferScroll/Offers/Offer{offer.Index}/Card");
            Mouse(item, MouseButton.Right); await Frames();
            var wealth = presenter.View.Player!.Wealth;
            if (!details.Visible || presenter.View.Player.Wealth != wealth) throw new InvalidOperationException("右键查看发生交易或未显示详情。");
            KeyInput(Key.Escape); await Frames();
            item = shell.GetNode<CardItemView>($"ContextRow/ContextHost/ShopView/OfferScroll/Offers/Offer{offer.Index}/Card");
            Mouse(item, MouseButton.Left); await Frames();
            if (presenter.View.Player!.Wealth != wealth - offer.Price) throw new InvalidOperationException("原生卡面点击没有正确购买。");
            offer = presenter.View.Offers.First(value => value.Action.Enabled);
            item = shell.GetNode<CardItemView>($"ContextRow/ContextHost/ShopView/OfferScroll/Offers/Offer{offer.Index}/Card");
            wealth = presenter.View.Player.Wealth; item.GrabFocus(); KeyInput(Key.Enter); await Frames();
            if (presenter.View.Player!.Wealth != wealth - offer.Price) throw new InvalidOperationException("Enter卡面购买失败或重复扣款。");
            var session = Session(presenter);
            session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card, "card.boar", "野猪", 1));
            presenter.RefreshView();
            var rewards = shell.GetNode<HeroDetailsView>("HeroDetails"); rewards.ShowSection(HeroSection.Rewards, shell.Size); await Frames();
            item = rewards.GetNode<CardItemView>("Content/Scroll/Items/RewardCard0"); item.GrabFocus(); wealth = presenter.View.Player.Wealth;
            KeyInput(Key.Space); await Frames();
            if (presenter.View.Rewards.Count != 0 || presenter.View.Player!.Wealth != wealth) throw new InvalidOperationException("Space卡面领取失败或改变金币。");
            rewards.Hide(); KeyInput(Key.Escape); await Frames();
            var ownedSaleCard = presenter.View.Player!.Cards.First(card => card.Key == new StringName("card.boar"));
            var salePlacement = presenter.View.Player.BoardPlacements.Single(placement => placement.CardId == ownedSaleCard.Id);
            var saleZone = salePlacement.Zone == BoardZone.Battlefield ? "BattlefieldRow" : "BenchRow";
            item = shell.GetNode<CardItemView>($"{saleZone}/Content/Board/Card_{ownedSaleCard.Id.Value:N}");
            wealth = presenter.View.Player.Wealth; item.GrabFocus(); KeyInput(Key.Enter); await Frames();
            var confirmSale = details.GetNode<Button>("Content/Sell");
            if (!details.Visible || details.CurrentCardId != ownedSaleCard.Id || !confirmSale.Visible || confirmSale.Disabled
                || presenter.View.Player.Wealth != wealth) throw new InvalidOperationException("Enter选中库存卡未打开出售确认或直接发生出售。");
            KeyInput(Key.Escape); await Frames();
            if (details.Visible || presenter.View.Player.Wealth != wealth || !presenter.View.Player.Cards.Any(card => card.Id == ownedSaleCard.Id))
                throw new InvalidOperationException("Escape取消出售改变了库存或金币。");
            item = shell.GetNode<CardItemView>($"{saleZone}/Content/Board/Card_{ownedSaleCard.Id.Value:N}");
            item.GrabFocus(); KeyInput(Key.Enter); await Frames();
            var saleFocusPath = new System.Collections.Generic.List<string>();
            for (var step = 0; step < 40 && !confirmSale.HasFocus(); step++)
            {
                saleFocusPath.Add(owner.GetViewport().GuiGetFocusOwner()?.GetPath().ToString() ?? "无焦点");
                KeyInput(Key.Tab); await Frames();
            }
            if (!confirmSale.HasFocus()) throw new InvalidOperationException("Tab无法到达出售确认：" + string.Join(" → ", saleFocusPath));
            await Save("sale-keyboard-confirm");
            KeyInput(Key.Enter); await Frames();
            if (details.Visible || presenter.View.Player.Wealth != wealth + ownedSaleCard.Value || presenter.View.Player.Cards.Any(card => card.Id == ownedSaleCard.Id))
                throw new InvalidOperationException($"Enter确认出售检查失败：详情{details.Visible}/{details.CurrentCardId}/{details.CurrentCardKey}，金币{presenter.View.Player.Wealth}/{wealth + ownedSaleCard.Value}，原卡存在{presenter.View.Player.Cards.Any(card => card.Id == ownedSaleCard.Id)}，焦点{owner.GetViewport().GuiGetFocusOwner()?.GetPath()}。");
            KeyInput(Key.Enter); await Frames();
            if (presenter.View.Player.Wealth != wealth + ownedSaleCard.Value) throw new InvalidOperationException("确认出售后重复Enter触发了额外交易。");
            GD.Print($"出售键盘确认通过：Enter只选中，Escape取消，Tab {saleFocusPath.Count}步到达确认，Enter只出售一次。");
            await owner.ToSignal(owner.GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
            var captured = presenter.View;
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080), new Vector2I(615, 440) })
            {
                owner.GetWindow().Size = size; owner.GetTree().Root.ContentScaleSize = size; await Frames();
                var sample = MatchDisplayQuery.FromOffer(ShopOffer.Create(new AscendantCardDefinition()));
                foreach (var progress in new[] { 0, 35, 75, 120 })
                {
                    var card = sample with { Id = EntityId.New(), Quests = Array.AsReadOnly(sample.QuestDefinitions.Select(quest =>
                        new QuestProgressSnapshot(quest.Key, Math.Min(progress, quest.RequiredCount), quest.RequiredCount, progress >= quest.RequiredCount)).ToArray()) };
                    shell.Render(captured with { SelectedCardId = card.Id, Player = captured.Player! with
                        { Cards = Array.AsReadOnly(captured.Player!.Cards.Append(card).ToArray()) } });
                    details.SetPinned(true); details.ShowNear(card, new Rect2(new Vector2(size.X * .45f, 80), new Vector2(60, 110)), size);
                    await Frames(); await owner.ToSignal(owner.GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
                    if (!details.Visible || details.GetGlobalRect().End.X > size.X - 11 * details.Scale.X || details.GetGlobalRect().End.Y > size.Y - 11 * details.Scale.Y
                        || details.Position.X < 12 * details.Scale.X || details.Position.Y < 12 * details.Scale.Y) throw new InvalidOperationException("任务详情越界。");
                    await Save($"quest-{progress}");
                }
                shell.SetDesktopPalette(true); await Save("quest-blue"); shell.SetDesktopPalette(false);
                var body = details.GetNode<ScrollContainer>("Content/CardContent/BodyScroll");
                body.ScrollVertical = (int)body.GetVScrollBar().MaxValue; await Frames();
                if (body.GetVScrollBar().MaxValue > body.GetVScrollBar().Page && body.ScrollVertical <= 0)
                    throw new InvalidOperationException("任务正文无法滚动至完整奖励。");
                await Save("quest-scrolled");
                KeyInput(Key.Escape); await Frames(); shell.Render(captured); await Frames();
                offer = captured.Offers.First(value => value.Action.Enabled);
                item = shell.GetNode<CardItemView>($"ContextRow/ContextHost/ShopView/OfferScroll/Offers/Offer{offer.Index}/Card");
                var shopScroll = shell.GetNode<ScrollContainer>("ContextRow/ContextHost/ShopView/OfferScroll");
                item.GrabFocus(); await Frames();
                var point = item.GetGlobalRect().GetCenter();
                if (!shopScroll.GetGlobalRect().HasPoint(point)) throw new InvalidOperationException("商品键盘焦点未自动滚动到可见区域。");
                owner.GetViewport().PushInput(new InputEventMouseMotion { Position = point }, true); await Frames();
                await owner.ToSignal(owner.GetTree().CreateTimer(.7), SceneTreeTimer.SignalName.Timeout);
                if (!details.Visible || details.CurrentCardKey != offer.Card.Key || details.MouseFilter != Control.MouseFilterEnum.Ignore)
                    throw new InvalidOperationException($"悬停详情未显示当前商品或阻挡商品点击：窗口{size}，页面{shell.Size}/{captured.Page}，详情{details.Visible}/{details.CurrentCardKey}/{details.MouseFilter}，卡牌{item.GetGlobalRect()}/{item.IsVisibleInTree()}，滚动{shopScroll.ScrollHorizontal}/{shopScroll.GetHScrollBar().MaxValue}/{shopScroll.GetHScrollBar().Page}，区域{shopScroll.GetGlobalRect()}，内容{shopScroll.GetNode<Control>("Offers").Size}/{shopScroll.GetNode<Control>("Offers").GetCombinedMinimumSize()}，焦点{item.HasFocus()}。");
                await Save("shop-hover");
                Mouse(item, MouseButton.Right); await Frames();
                owner.GetViewport().PushInput(new InputEventMouseMotion { Position = details.GetGlobalRect().GetCenter() }, true); await Frames();
                if (!details.Visible || details.MouseFilter != Control.MouseFilterEnum.Stop || details.CurrentCardKey != offer.Card.Key)
                    throw new InvalidOperationException("右键固定后移入详情使面板丢失。");
                Mouse(details.GetNode<Button>("Close"), MouseButton.Left); await Frames();
                if (details.Visible) throw new InvalidOperationException("实际点击关闭按钮没有关闭详情。");
                item = shell.GetNode<CardItemView>($"ContextRow/ContextHost/ShopView/OfferScroll/Offers/Offer{offer.Index}/Card");
                item.GrabFocus(); await Frames();
                owner.GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.One }, true); await Frames();
                owner.GetViewport().PushInput(new InputEventMouseMotion { Position = item.GetGlobalRect().GetCenter() }, true); await Frames();
                if (!details.Visible || details.MouseFilter != Control.MouseFilterEnum.Ignore) throw new InvalidOperationException("关闭后不能重新悬停详情。");
                for (var attempt = 0; attempt < 12; attempt++)
                {
                    if (attempt > 0)
                    {
                        item.GrabFocus(); await Frames();
                        owner.GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.One }, true); await Frames();
                        owner.GetViewport().PushInput(new InputEventMouseMotion { Position = item.GetGlobalRect().GetCenter() }, true); await Frames();
                        if (!details.Visible) throw new InvalidOperationException("Escape后不能重新查看详情。");
                    }
                    KeyInput(Key.Escape); await Frames();
                    if (details.Visible || !ReferenceEquals(item, shell.GetNode<CardItemView>($"ContextRow/ContextHost/ShopView/OfferScroll/Offers/Offer{offer.Index}/Card")))
                        throw new InvalidOperationException($"Escape关闭后详情重新打开或商品重建：窗口{size}，第{attempt + 1}次，焦点{owner.GetViewport().GuiGetFocusOwner()?.GetPath()}。");
                }
                GD.Print($"{size}连续12次Escape关闭与重新查看详情通过，商品节点保持。");
            }
            GD.Print("卡牌原生右键、鼠标购买、Enter购买、Space奖励领取及三种窗口任务截图通过。");
        }
        finally { owner.RemoveChild(scene); scene.Free(); }
        async Task Frames() { for (var i = 0; i < 8; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame); }
        void Mouse(Control control, MouseButton button)
        {
            var point = control.GetGlobalRect().GetCenter();
            owner.GetViewport().PushInput(new InputEventMouseMotion { Position = point }, true);
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = button, Pressed = true }, true);
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = button, Pressed = false }, true);
        }
        void KeyDown(Key key) => owner.GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = true }, true);
        void KeyUp(Key key) => owner.GetViewport().PushInput(new InputEventKey { Keycode = key, Pressed = false }, true);
        void KeyInput(Key key) { KeyDown(key); KeyUp(key); }
        async Task Feedback(Button button, string name)
        {
            if (button.GetNodeOrNull<ButtonFeedback>("InteractionFeedback") is null) throw new InvalidOperationException($"{name}缺少交互反馈层。");
            owner.GetViewport().GuiReleaseFocus();
            owner.GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.Zero }, true); await Settle();
            var normal = await Pixels(button); await Save($"feedback-{name}-normal");
            var point = button.GetGlobalRect().GetCenter();
            owner.GetViewport().PushInput(new InputEventMouseMotion { Position = point }, true); await Settle();
            var hover = await Pixels(button); await Save($"feedback-{name}-hover");
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = true }, true); await Frames();
            var down = await Pixels(button); await Save($"feedback-{name}-down");
            if (normal.SequenceEqual(hover) || hover.SequenceEqual(down)) throw new InvalidOperationException($"{name}实际渲染缺少悬停／按下变化。");
            // 移出后释放取消点击，避免视觉验证触发页面命令。
            owner.GetViewport().PushInput(new InputEventMouseMotion { Position = Vector2.Zero }, true);
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = Vector2.Zero, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            owner.GetViewport().GuiReleaseFocus(); await Settle();
            var restored = await Pixels(button);
            await Save($"feedback-{name}-restored");
            if (!normal.SequenceEqual(restored)) throw new InvalidOperationException($"{name}松开／移出后未恢复普通状态。");
            GD.Print($"{name}真实鼠标悬停、按下、移出释放及恢复的渲染反馈通过。");
        }
        async Task Settle() { await owner.ToSignal(owner.GetTree().CreateTimer(.18), SceneTreeTimer.SignalName.Timeout); await Frames(); }
        async Task<byte[]> Pixels(Control control)
        {
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = owner.GetViewport().GetTexture().GetImage();
            var scale = (Vector2)image.GetSize() / owner.GetViewportRect().Size;
            var rect = control.GetGlobalRect();
            using var region = image.GetRegion(new Rect2I((Vector2I)(rect.Position * scale), (Vector2I)(rect.Size * scale)));
            return region.GetData();
        }
        async Task Save(string label)
        {
            if (label.StartsWith("quest", StringComparison.Ordinal) && !detailsVisible()) throw new InvalidOperationException("保存任务截图时详情已隐藏。");
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            const string directory = "res://output/card-interactions"; DirAccess.MakeDirRecursiveAbsolute(directory);
            using var image = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng($"{directory}/{label}-{image.GetWidth()}x{image.GetHeight()}.png") != Error.Ok) throw new InvalidOperationException("交互截图保存失败。");
        }
        bool detailsVisible() => scene.GetNode<CardDetailsView>("MatchShell/CardDetails").Visible;
    }

    private static MinimalPlaytest Create(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        owner.AddChild(scene); scene.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); return scene;
    }
    private static MatchPresenter Presenter(MinimalPlaytest scene) => (MatchPresenter)typeof(MinimalPlaytest)
        .GetField("_presenter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene)!;
    private static MatchSession Session(MatchPresenter presenter) => (MatchSession)typeof(MatchPresenter)
        .GetField("_player", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(presenter)!;
}
