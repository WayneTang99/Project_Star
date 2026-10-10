using System;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using System.Collections.Generic;
using Project_Star.Application.Content;
using Project_Star.Domain.Match;
using Project_Star.Domain.Combat;

namespace Project_Star.Presentation.Playtest;

// 16:9双列排版；怪物及其战场在上，英雄及其战场在下，子视图仅消费快照。
public sealed partial class MatchShell : Control
{
    public event Action<MatchPage, StringName, long>? ChoiceSelected;
    public event Action<int, long>? BuyRequested;
    public event Action<MatchAction>? ActionRequested;
    public event Action<BoardZone, int>? BoardSlotPressed;
    public event Action<BoardDragData, BoardZone, int>? MoveRequested;
    public event Func<BoardDragData, BoardZone, int, Result<BoardPlacementResult>>? MovePreviewRequested;
    public event Action? CancelRequested;
    public event Action<Guid, Project_Star.Domain.Common.EntityId, int, int>? SellRequested;
    public event Action<Guid, Project_Star.Domain.Common.EntityId, int, int>? DragSellRequested;
    public event Func<BoardDragData, Result<int>>? SellPreviewRequested;
    public event Action<Guid, int, long>? ClaimRequested;
    private TopBar _top = null!;
    private ContextPortrait _portrait = null!;
    private PlayerHeroPanel _hero = null!;
    private PlayerProgressPanel _progress = null!;
    private LeavePanel _leave = null!;
    private ScrollContainer _leaveScroll = null!;
    private HeroSelectionView _heroes = null!;
    private CardCatalogView _catalog = null!;
    private SkillCatalogView _skillCatalog = null!;
    private Button _menuButton = null!;
    private MatchMenuView _menu = null!;
    private EconomyView _economy = null!;
    private DisplaySettingsView _settings = null!;
    private EncounterSelectionView _encounters = null!;
    private KeyedActionView _events = null!;
    private ShopView _shop = null!;
    private ResultView _result = null!;
    private BoardZoneView _battlefield = null!;
    private BoardZoneView _bench = null!;
    private BoardZoneView _enemy = null!;
    private CardDetailsView _details = null!;
    private HeroDetailsView _secondary = null!;
    private SellDropZone _sellDrop = null!;
    private MatchPage? _page;
    private int _capacity = 10;
    private VBoxContainer _footerActions = null!;
    private HBoxContainer _navigation = null!;
    private Rect2 _stageRect;
    private Rect2 _heroRect;
    private VScrollBar _desktopScroll = null!;
    private HBoxContainer _bottomLine = null!;
    private HBoxContainer _heroStats = null!;
    private Label _heroArmor = null!;
    private Label _heroRegen = null!;
    private MatchPageViewModel? _view;
    private float _documentHeight;
    private bool _layingOut;
    private Control? _lastFocus;
    private bool _pinnedDetails;
    private BattleStageView _battleStage = null!;
    private PanelContainer _toast = null!;
    private Label _toastText = null!;
    private Tween? _toastTween;

    public override void _Ready()
    {
        // HTML 按 sRGB 合成透明层，纯界面视口使用相同的合成空间。
        GetViewport().UseHdr2D = false;
        Theme = MatchTheme.Create();
        var background = new ColorRect { Name = "Background", Color = MatchTheme.Background,
            MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
        AddChild(background); MoveChild(background, 0);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _top = GetNode<TopBar>("TopBar");
        _portrait = Add(GetNode<Control>("ContextRow/Portrait"), new ContextPortrait { Name = "ContextPortrait" });
        _hero = Add(GetNode<Control>("BenchRow/Hero"), new PlayerHeroPanel { Name = "PlayerHeroPanel" });
        _progress = Add(GetNode<Control>("BenchRow/Progress"), new PlayerProgressPanel { Name = "PlayerProgressPanel" });
        _leaveScroll = Add(GetNode<Control>("ContextRow/Leave"), new ScrollContainer
            { Name = "LeaveScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true });
        _leave = Add(_leaveScroll, new LeavePanel { Name = "LeavePanel", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _footerActions = Add(this, new VBoxContainer { Name = "FooterActions" });
        _footerActions.AddThemeConstantOverride("separation", 4);
        _leave.SetFooterHost(_footerActions);
        _desktopScroll = Add(this, new VScrollBar { Name = "DesktopScroll", ZIndex = 8, Step = 1 });
        _desktopScroll.ValueChanged += ScrollChanged;
        _bottomLine = Add(this, new HBoxContainer { Name = "BottomLine" });
        var footerText = new Label { Text = "小 / 中 / 大型卡牌共用构图", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        MatchTheme.Text(footerText, 10, new Color("748d7f"), spacing: 1); _bottomLine.AddChild(footerText);
        var paletteText = new Label { Text = "桌面配色" }; MatchTheme.Text(paletteText, 10, new Color("748d7f")); _bottomLine.AddChild(paletteText);
        foreach (var (title, blue) in new[] { ("森林金", false), ("星辉蓝", true) })
        {
            var button = new Button { Text = title }; button.AddThemeFontSizeOverride("font_size", 10);
            _bottomLine.AddChild(button); button.Pressed += () => SetDesktopPalette(blue);
        }
        _heroStats = Add(this, new HBoxContainer { Name = "HeroStats" }); _heroStats.AddThemeConstantOverride("separation", 20);
        var armor = new HBoxContainer(); var regen = new HBoxContainer(); _heroStats.AddChild(armor); _heroStats.AddChild(regen);
        var armorTitle = new Label { Text = "护甲" }; MatchTheme.Text(armorTitle, 11, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.Armor)); armor.AddChild(armorTitle);
        _heroArmor = new Label(); MatchTheme.Text(_heroArmor, 17, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.Armor), true); armor.AddChild(_heroArmor);
        var regenTitle = new Label { Text = "再生" }; MatchTheme.Text(regenTitle, 11, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.HealthRegen)); regen.AddChild(regenTitle);
        _heroRegen = new Label(); MatchTheme.Text(_heroRegen, 17, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.HealthRegen), true); regen.AddChild(_heroRegen);
        _navigation = Add(this, new HBoxContainer { Name = "StageNavigation", MouseFilter = MouseFilterEnum.Ignore });
        _navigation.AddThemeConstantOverride("separation", 24);
        foreach (var title in new[] { "商店", "遭遇", "战斗" })
        {
            var label = new Label { Text = title, CustomMinimumSize = new Vector2(36, 39), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            MatchTheme.Text(label, 14, MatchTheme.Muted, spacing: 2); _navigation.AddChild(label);
        }
        var host = GetNode<Control>("ContextRow/ContextHost");
        _heroes = Add(host, new HeroSelectionView { Name = "HeroSelectionView" });
        _catalog = Add(this, new CardCatalogView { Name = "CardCatalog", ZIndex = 30 });
        _catalog.Closed += CloseCatalog;
        _skillCatalog = Add(this, new SkillCatalogView { Name = "SkillCatalog", ZIndex = 30 });
        _skillCatalog.Closed += CloseSkillCatalog;
        _menuButton = Add(this, new Button { Name = "OpenGameMenu", Text = "☰ 菜单", ZIndex = 5 });
        _menuButton.Pressed += OpenMenu;
        _menu = Add(this, new MatchMenuView { Name = "GameMenu", ZIndex = 45 });
        _menu.SettingsRequested += OpenSettings; _menu.CardsRequested += OpenCatalog; _menu.SkillsRequested += OpenSkillCatalog;
        _menu.AbandonRequested += Abandon;
        _economy = Add(this, new EconomyView { Name = "Economy" });
        _settings = Add(this, new DisplaySettingsView { Name = "DisplaySettings", ZIndex = 50 });
        _settings.PaletteChanged += SetDesktopPalette;
        _encounters = Add(host, new EncounterSelectionView { Name = "EncounterChoiceView" });
        _events = Add(host, new KeyedActionView { Name = "EventView" });
        _shop = Add(host, new ShopView { Name = "ShopView" });
        _result = Add(host, new ResultView { Name = "ResultView" });
        _enemy = Add(host, new BoardZoneView { Name = "EnemyBoard", ShowHeading = false });
        _battleStage = Add(host, new BattleStageView { Name = "BattleStage" });
        _battleStage.Requested += RequestAction;
        _battlefield = Add(GetNode<Control>("BattlefieldRow/Content"), new BoardZoneView { Name = "Board" });
        _bench = Add(GetNode<Control>("BenchRow/Content"), new BoardZoneView { Name = "Board" });
        _details = Add(this, new CardDetailsView { Name = "CardDetails", ZIndex = 10 });
        _secondary = Add(this, new HeroDetailsView { Name = "HeroDetails", ZIndex = 9 });
        _toast = Add(this, new PanelContainer { Name = "Toast", ZIndex = 40, MouseFilter = MouseFilterEnum.Ignore, Visible = false });
        var toastPlate = MatchTheme.Surface(new Color("243a2f"), MatchTheme.Gold);
        toastPlate.ContentMarginLeft = toastPlate.ContentMarginRight = 24; toastPlate.ContentMarginTop = toastPlate.ContentMarginBottom = 12;
        _toast.AddThemeStyleboxOverride("panel", toastPlate);
        _toastText = new Label(); MatchTheme.Text(_toastText, 14, new Color("eedab1")); _toast.AddChild(_toastText);
        _sellDrop = Add(this, new SellDropZone { Name = "SellDropZone", ZIndex = 20 });
        _sellDrop.PreviewRequested = PreviewSale;
        _sellDrop.DropRequested += DropSale;
        _heroes.Selected += SelectHero; _encounters.Selected += SelectEncounter; _events.Selected += SelectEvent;
        _shop.BuyRequested += Buy; _leave.Requested += RequestAction;
        _shop.DetailsRequested += ShowDetails; _details.SellRequested += Sell; _details.Closed += CloseDetails;
        _result.ClaimRequested += Claim; _result.DetailsRequested += ShowDetails;
        _hero.SectionRequested += ShowSection; _secondary.ClaimRequested += Claim; _secondary.DetailsRequested += ShowDetails;
        _battlefield.SlotPressed += BattlefieldSlot; _bench.SlotPressed += BenchSlot;
        _battlefield.PreviewRequested = PreviewMove; _bench.PreviewRequested = PreviewMove;
        _battlefield.DropRequested += DropMove; _bench.DropRequested += DropMove;
        _battlefield.DetailsRequested += ShowDetails; _bench.DetailsRequested += ShowDetails; _enemy.DetailsRequested += ShowDetails;
        Resized += LayoutShell; LayoutShell();
        GetTree().NodeAdded += AddButtonFeedback;
        foreach (var button in FindChildren("*", "BaseButton", true, false).OfType<BaseButton>()) AddButtonFeedback(button);
    }

    // 子组件共享同一页面捕获，只有当前中央页面可见。
    public void Render(MatchPageViewModel view)
    {
        var previousMessage = _view?.Message;
        if (_view?.Player?.MatchId != view.Player?.MatchId) { _menu.Close(); _settings.Hide(); _catalog.Hide(); _skillCatalog.Hide(); }
        _view = view;
        if (view.Message != previousMessage && System.Text.RegularExpressions.Regex.IsMatch(view.Message, "成功|失败|不足|无法|不能|已购买|已出售|已领取|已售罄|已满"))
        {
            _toastText.Text = view.Message; _toast.Size = Vector2.Zero; _toast.Show(); _toast.Modulate = Colors.White;
            _toastTween?.Kill(); _toastTween = CreateTween(); _toastTween.TweenInterval(2.2); _toastTween.TweenProperty(_toast, "modulate:a", 0f, .2);
            _toastTween.TweenCallback(Callable.From(_toast.Hide));
        }
        var previousDetailId = _details.CurrentCardId;
        var preserveDetail = _details.Visible && previousDetailId is not null && _page == view.Page;
        _pinnedDetails = view.SelectedCardId is not null || (_pinnedDetails && preserveDetail);
        if (!preserveDetail) _details.Hide();
        _sellDrop.Render(view.Player, view.BoardEnabled);
        _secondary.Render(view);
        _top.Render(view.Player, view.DisplayRound, view.DisplayTurn);
        _economy.Render(view.Player);
        _portrait.Render(view.Playback is null ? view.Title : view.Enemy?.Hero?.DisplayName ?? "敌方",
            view.EncounterLevel, view.Page is MatchPage.Preparation or MatchPage.BattlePlayback ? view.Enemy?.Hero : null,
            view.Playback?.State.Opponent);
        _portrait.SetContext(view.ContextIllustration, view.Message);
        _portrait.SetEyebrow($"第 {(view.DisplayTurn > 0 ? view.DisplayTurn : view.Player?.Turn ?? 0)} 回合 · {(view.Page == MatchPage.Shop ? $"{view.ShopLevel} 级商店" : view.Page is MatchPage.Preparation or MatchPage.BattlePlayback ? "怪物遭遇" : "旅途抉择")}");
        var activeNavigation = view.Page == MatchPage.Shop ? 0
            : view.Page is MatchPage.Preparation or MatchPage.BattlePlayback or MatchPage.BattleResult or MatchPage.MatchEnded ? 2 : 1;
        for (var index = 0; index < _navigation.GetChildCount(); index++)
            _navigation.GetChild<Label>(index).AddThemeColorOverride("font_color", index == activeNavigation ? MatchTheme.Gold : MatchTheme.Muted);
        _hero.Render(view.Player?.Hero); _progress.Render(view.Player); _leave.Render(view);
        _hero.RenderBattle(view.Playback?.State.Player, view.Playback);
        _hero.RenderSkills(view.Player?.Skills ?? Array.Empty<SkillSnapshot>(), view.Player?.Sets.Count ?? 0);
        _heroArmor.Text = (view.Playback?.State.Player.Armor ?? view.Player?.Hero?.CombatValues.GetValueOrDefault(Project_Star.Domain.Common.GameAttributeKeys.Armor) ?? 0).ToString();
        _heroRegen.Text = (view.Player?.Hero?.CombatValues.GetValueOrDefault(Project_Star.Domain.Common.GameAttributeKeys.HealthRegen) ?? 0).ToString();
        _heroes.Render(view.Message, view.Heroes);
        _encounters.Render(view.Message, view.Choices);
        _events.Render(view.Message, view.EventOptions, view.EventRevision, view.ContextIllustration, view.EncounterLevel);
        _shop.Render(view.Message, view.Offers, view.ShopLevel);
        if (view.Page is MatchPage.BattleResult or MatchPage.MatchEnded) _result.Render(view);
        var browsing = _catalog.Visible || _skillCatalog.Visible;
        _heroes.Visible = view.Page == MatchPage.HeroSelection && !browsing;
        _encounters.Visible = view.Page == MatchPage.EncounterChoice;
        _events.Visible = view.Page == MatchPage.Event;
        _shop.Visible = view.Page == MatchPage.Shop;
        _result.Visible = view.Page is MatchPage.BattleResult or MatchPage.MatchEnded;
        var capacity = Math.Max(view.Player?.BattlefieldCapacity ?? 0,
            Math.Max(view.Player?.BenchCapacity ?? 0, view.Enemy?.BattlefieldCapacity ?? 0));
        var capacityChanged = capacity > 0 && _capacity != capacity;
        if (capacity > 0) _capacity = capacity;
        _battlefield.SharedCapacity = _bench.SharedCapacity = _enemy.SharedCapacity = capacity;
        _battlefield.Render(view.Player, BoardZone.Battlefield, view.BoardEnabled, view.SelectedCardId, "战场");
        _bench.Render(view.Player, BoardZone.Bench, view.BoardEnabled, view.SelectedCardId, "备战");
        _enemy.Render(view.EnemyVisible && view.Page is MatchPage.Preparation or MatchPage.BattlePlayback ? view.Enemy : null,
            BoardZone.Battlefield, false, null, view.Playback is null ? "敌方战场"
                : $"战斗 {view.Playback.Tick / 10m:0.0}s{(view.Playback.Paused ? " · 暂停" : "")}{(view.Playback.State.Eclipse ? " · 日蚀" : "")}");
        _battleStage.Visible = view.Page is MatchPage.Preparation or MatchPage.BattlePlayback;
        if (_battleStage.Visible) _battleStage.Render(view);
        _enemy.Visible = _battleStage.Visible;
        _portrait.Visible = !_battleStage.Visible;
        _battlefield.RenderBattle(view.Playback); _enemy.RenderBattle(view.Playback); _bench.RenderBattle(view.Playback);
        if (_page != view.Page) { _battlefield.ClearFeedback(); _bench.ClearFeedback(); _enemy.ClearFeedback(); }
        var pageChanged = _page != view.Page;
        _page = view.Page;
        if (pageChanged || capacityChanged) LayoutShell();
        if (view.Playback is not null) _progress.RenderPlayback(string.Join("\n", view.Playback.Feedback));
        var selected = view.Player?.Cards.FirstOrDefault(card => card.Id == view.SelectedCardId);
        if (selected is not null)
        {
            _secondary.Hide();
            _details.ShowSelected(selected, view.Player!.MatchId, view.Sell, GetLocalMousePosition(), Size, BattleState(selected));
        }
        else if (preserveDetail && previousDetailId is { } detailId)
        {
            var refreshed = view.Player?.Cards.FirstOrDefault(card => card.Id == detailId)
                ?? view.Enemy?.Cards.FirstOrDefault(card => card.Id == detailId)
                ?? view.Offers.Select(offer => offer.Card).Concat(view.Rewards.Where(reward => reward.Card is not null).Select(reward => reward.Card!))
                    .FirstOrDefault(card => card.Id == detailId && card.Key == _details.CurrentCardKey);
            if (refreshed is not null)
                _details.RefreshCard(refreshed, Size, view.Playback?.State.Cards.FirstOrDefault(state => state.Id == detailId));
            else
            {
                _details.Hide();
                _pinnedDetails = false;
            }
        }
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= AddButtonFeedback;
        _desktopScroll.ValueChanged -= ScrollChanged;
        _toastTween?.Kill();
        _battleStage.Requested -= RequestAction;
        Resized -= LayoutShell;
        _catalog.Closed -= CloseCatalog; _skillCatalog.Closed -= CloseSkillCatalog;
        _menuButton.Pressed -= OpenMenu;
        _menu.SettingsRequested -= OpenSettings; _menu.CardsRequested -= OpenCatalog; _menu.SkillsRequested -= OpenSkillCatalog;
        _menu.AbandonRequested -= Abandon;
        _settings.PaletteChanged -= SetDesktopPalette;
        _heroes.Selected -= SelectHero; _encounters.Selected -= SelectEncounter; _events.Selected -= SelectEvent;
        _shop.BuyRequested -= Buy; _leave.Requested -= RequestAction;
        _shop.DetailsRequested -= ShowDetails; _details.SellRequested -= Sell; _details.Closed -= CloseDetails;
        _result.ClaimRequested -= Claim; _result.DetailsRequested -= ShowDetails;
        _hero.SectionRequested -= ShowSection; _secondary.ClaimRequested -= Claim; _secondary.DetailsRequested -= ShowDetails;
        _battlefield.SlotPressed -= BattlefieldSlot; _bench.SlotPressed -= BenchSlot;
        _battlefield.PreviewRequested = null; _bench.PreviewRequested = null;
        _battlefield.DropRequested -= DropMove; _bench.DropRequested -= DropMove;
        _battlefield.DetailsRequested -= ShowDetails; _bench.DetailsRequested -= ShowDetails; _enemy.DetailsRequested -= ShowDetails;
        _sellDrop.PreviewRequested = null;
        _sellDrop.DropRequested -= DropSale;
    }

    private void SelectHero(StringName key, long revision) => ChoiceSelected?.Invoke(MatchPage.HeroSelection, key, revision);
    private void SelectEncounter(StringName key, long revision) => ChoiceSelected?.Invoke(MatchPage.EncounterChoice, key, revision);
    private void SelectEvent(StringName key, long revision) => ChoiceSelected?.Invoke(MatchPage.Event, key, revision);
    private void Buy(int index, long revision) => BuyRequested?.Invoke(index, revision);
    private void RequestAction(MatchAction action) => ActionRequested?.Invoke(action);
    private void OpenMenu()
    {
        if (_settings.Visible || _catalog.Visible || _skillCatalog.Visible) return;
        _details.Hide(); _secondary.Hide();
        _menu.Open(_view?.Player?.MatchId ?? Guid.Empty, _view?.Player?.Status == MatchStatus.InProgress, _menuButton);
    }
    public event Action<Guid>? AbandonRequested;
    private void Abandon(Guid matchId) => AbandonRequested?.Invoke(matchId);
    private void OpenSettings() { if (!_catalog.Visible && !_skillCatalog.Visible) _settings.Open(); }
    // 入口在真正启动游戏时载入显示配置，验证夹具不修改用户窗口设置。
    public void LoadDisplaySettings() => _settings.LoadSaved();
    // 图鉴内容由入口的应用查询注入，不读取对局状态。
    public void SetCatalog(IReadOnlyList<CardCatalogEntry> entries) => _catalog.SetEntries(entries);
    // 技能图鉴同样只接收应用查询的冻结条目。
    public void SetSkillCatalog(IReadOnlyList<SkillCatalogEntry> entries) => _skillCatalog.SetEntries(entries);
    private void OpenCatalog()
    {
        _menu.Close(); _details.Hide(); _secondary.Hide(); _heroes.Hide();
        _settings.Hide(); _skillCatalog.Hide(); _catalog.Open();
    }
    private void CloseCatalog()
    {
        _heroes.Visible = _page == MatchPage.HeroSelection; _menuButton.GrabFocus();
    }
    private void OpenSkillCatalog()
    {
        _menu.Close(); _details.Hide(); _secondary.Hide(); _heroes.Hide();
        _settings.Hide(); _catalog.Hide(); _skillCatalog.Open();
    }
    private void CloseSkillCatalog()
    {
        _heroes.Visible = _page == MatchPage.HeroSelection; _menuButton.GrabFocus();
    }
    // 开发日志的打开与关闭不发出对局命令。
    public void ToggleLog() => _result.ToggleLog();
    private void Sell(Guid matchId, Project_Star.Domain.Common.EntityId id, int level, int value) => SellRequested?.Invoke(matchId, id, level, value);
    private void Claim(Guid matchId, int index, long revision) => ClaimRequested?.Invoke(matchId, index, revision);
    private void ShowSection(HeroSection section)
    { CancelRequested?.Invoke(); _details.Hide(); _secondary.ShowSection(section, Size); }
    private void BattlefieldSlot(int slot) => BoardSlotPressed?.Invoke(BoardZone.Battlefield, slot);
    private void BenchSlot(int slot) => BoardSlotPressed?.Invoke(BoardZone.Bench, slot);
    private CardBattleSnapshot? BattleState(CardSnapshot card) => _view?.Playback?.State.Cards.FirstOrDefault(state => state.Id == card.Id);
    private void ShowDetails(CardSnapshot card, Rect2 rect)
    { _pinnedDetails = true; _details.SetPinned(true); _details.ShowNear(card, new Rect2(rect.Position - GlobalPosition, rect.Size), Size, BattleState(card)); }
    private void CloseDetails() { _pinnedDetails = false; CancelRequested?.Invoke(); }
    // 卡牌悬停立即复用固定详情的内容组件，定位依据卡牌可见边界。
    public void ShowHoverCard(CardSnapshot card, Rect2 rect, CardBattleSnapshot? battle = null)
    { if (!_pinnedDetails && !_menu.Visible && !_settings.Visible && !_catalog.Visible && !_skillCatalog.Visible && !GetViewport().GuiIsDragging()) { _details.SetPinned(false); _details.ShowNear(card, new Rect2(rect.Position - GlobalPosition, rect.Size), Size, battle ?? BattleState(card)); } }
    // 固定详情不随卡牌离开而关闭。
    public void HideHoverCard() { if (!_pinnedDetails) _details.Hide(); }
    private void DropMove(BoardDragData data, BoardZone zone, int start) => MoveRequested?.Invoke(data, zone, start);
    private void DropSale(BoardDragData data, CardSnapshot card) =>
        DragSellRequested?.Invoke(data.MatchId, data.CardId, card.Level, card.Value);
    private Result<int> PreviewSale(BoardDragData data) => SellPreviewRequested?.Invoke(data)
        ?? Result<int>.Fail(new Failure(new StringName("ui.sale_unavailable"), "当前不可出售。"));
    private Result<BoardPlacementResult> PreviewMove(BoardDragData data, BoardZone zone, int start) =>
        MovePreviewRequested?.Invoke(data, zone, start) ?? Result<BoardPlacementResult>.Fail(
            new Failure(new StringName("ui.preview_unavailable"), "当前区域不可移动。"));

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape })
        { _pinnedDetails = false; _details.Hide(); _secondary.Hide(); CancelRequested?.Invoke(); GetViewport().SetInputAsHandled(); }
    }

    private static T Add<T>(Control parent, T child) where T : Control { parent.AddChild(child); return child; }

    private void AddButtonFeedback(Node node)
    {
        if (node is not BaseButton button || !IsAncestorOf(button)) return;
        // NodeAdded期间节点仍在进入树，延后挂载装饰以免修改中的树重入。
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree() || !IsAncestorOf(button)
                || button.GetNodeOrNull<ButtonFeedback>("InteractionFeedback") is not null) return;
            button.AddChild(new ButtonFeedback { Name = "InteractionFeedback" });
        }).CallDeferred();
    }

    private void LayoutShell()
    {
        if (_layingOut || _desktopScroll is null) return;
        _layingOut = true;
        const float margin = 24;
        var choosing = _page == MatchPage.HeroSelection;
        var width = Size.X - margin * 2;
        var side = width * 440 / 1552;
        const float gap = 24;
        var mainLeft = margin + side + gap;
        var mainWidth = width - side - gap;
        const float top = 136;
        var capacity = Math.Max(1, _capacity);
        // 原棋盘按宽度保持小型1:2比例；阶段和两排棋盘不等分高度。
        var boardHeight = 27 + 20 + (mainWidth - 20 - 6 * (capacity - 1)) / capacity * 2;
        var enemyHeight = 20 + (mainWidth - 2 - 20 - 6 * (capacity - 1)) / capacity * 2;
        var battle = _battleStage.Visible;
        var phaseHeight = battle ? enemyHeight + 2 : 214;
        var heroTop = battle ? top + 258 : top;
        var heroHeight = battle ? 279 : 245;
        const float stageTitleWidth = 210;
        var hostLeft = battle ? mainLeft : mainLeft + stageTitleWidth;
        var hostWidth = battle ? mainWidth : mainWidth - stageTitleWidth;
        _documentHeight = Size.Y;
        _desktopScroll.MaxValue = _desktopScroll.Page = Size.Y;
        _desktopScroll.Value = 0; _desktopScroll.Hide();
        _top.Position = new Vector2(margin, 0); _top.Size = new Vector2(width, 82);
        _navigation.Position = new Vector2(margin, 89.5f); _navigation.Size = new Vector2(width, 39);
        _stageRect = new Rect2(mainLeft, top, mainWidth, phaseHeight);
        _heroRect = new Rect2(margin, heroTop, side, heroHeight);
        foreach (var path in new[] { "ContextRow", "BattlefieldRow", "BenchRow" })
        {
            var row = GetNode<Control>(path); row.Position = Vector2.Zero; row.Size = Size;
            row.MouseFilter = MouseFilterEnum.Ignore;
            foreach (Control cell in row.GetChildren()) cell.MouseFilter = MouseFilterEnum.Ignore;
        }
        Place("ContextRow/Portrait", mainLeft + 1, top + 1, stageTitleWidth - 1, phaseHeight - 2);
        _portrait.Size = GetNode<Control>("ContextRow/Portrait").Size;
        Place("ContextRow/Leave", battle ? margin + 24 : mainLeft + 24,
            battle ? heroTop + heroHeight + 56 : top + phaseHeight / 2 + 48, battle ? side - 48 : stageTitleWidth - 40, 76);
        _leaveScroll.Size = GetNode<Control>("ContextRow/Leave").Size;
        Place("ContextRow/ContextHost", hostLeft, top, hostWidth, phaseHeight);
        Place("BattlefieldRow/Content", mainLeft, top + phaseHeight + 12, mainWidth, boardHeight);
        Place("BenchRow/Content", mainLeft, top + phaseHeight + boardHeight + 24, mainWidth, boardHeight);
        foreach (var view in new Control[] { _heroes, _encounters, _events, _shop, _result })
        { view.Position = Vector2.Zero; view.Size = GetNode<Control>("ContextRow/ContextHost").Size; }
        _enemy.Position = Vector2.One; _enemy.Size = new Vector2(mainWidth - 2, enemyHeight);
        // 复用原怪物摘要，仅移至左上；敌方棋盘独立占据右上。
        _battleStage.Position = new Vector2(margin - mainLeft, 0); _battleStage.Size = new Vector2(side, 242);
        _battlefield.Size = _bench.Size = new Vector2(mainWidth, boardHeight);
        Place("BenchRow/Hero", margin + 24, heroTop + 11, side - 48, 123);
        _hero.Size = GetNode<Control>("BenchRow/Hero").Size;
        var extraStatusHeight = battle ? 33 : 0;
        Place("BenchRow/Progress", margin + 155, heroTop + 131 + extraStatusHeight, side - 179, 16);
        _progress.Size = GetNode<Control>("BenchRow/Progress").Size;
        _heroStats.Position = new Vector2(margin + 24, heroTop + 170 + extraStatusHeight); _heroStats.Size = new Vector2(side - 48, 22);
        _footerActions.Position = new Vector2(margin + 24, heroTop + 199 + extraStatusHeight); _footerActions.Size = new Vector2(side - 48, 36);
        _bottomLine.Position = new Vector2(margin, heroTop + heroHeight + 15); _bottomLine.Size = new Vector2(side, 22);
        GetNode<Control>("BattlefieldRow").Visible = GetNode<Control>("BenchRow").Visible = !choosing;
        GetNode<Control>("ContextRow/Portrait").Visible = GetNode<Control>("ContextRow/Leave").Visible = !choosing;
        _navigation.Visible = _footerActions.Visible = _bottomLine.Visible = _heroStats.Visible = !choosing;
        if (choosing)
        {
            var host = GetNode<Control>("ContextRow/ContextHost"); host.Position = new Vector2(margin, 90);
            host.Size = new Vector2(width, Size.Y - 108); _heroes.Size = host.Size;
        }
        _menuButton.Position = new Vector2(Size.X - margin - 96, 18); _menuButton.Size = new Vector2(96, 40);
        _menu.Position = Vector2.Zero; _menu.Size = Size;
        _economy.Position = new Vector2(margin, Size.Y - 14 - 68); _economy.Size = new Vector2(side, 68);
        _settings.Position = Vector2.Zero; _settings.Size = Size;
        _catalog.Position = new Vector2(margin, margin); _catalog.Size = Size - Vector2.One * margin * 2;
        _skillCatalog.Position = _catalog.Position; _skillCatalog.Size = _catalog.Size;
        _details.ClampTo(Size); _secondary.ClampTo(Size);
        _sellDrop.Position = _stageRect.Position + Vector2.One * 8; _sellDrop.Size = _stageRect.Size - Vector2.One * 16;
        _portrait.Visible = !choosing && !_battleStage.Visible;
        _layingOut = false; QueueRedraw();
    }

    private void ScrollChanged(double value) { if (!_pinnedDetails) _details.Hide(); LayoutShell(); }
    // 固定16:9画布隐藏整页滚动；浮层和图鉴自行消费滚轮。
    public override void _Input(InputEvent input)
    {
        if (!_desktopScroll.Visible || _catalog.Visible || _skillCatalog.Visible || _secondary.Visible
            || _details.Visible && _details.GetGlobalRect().HasPoint(GetGlobalMousePosition())) return;
        if (input is InputEventMouseButton { Pressed: true } mouse && mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            _desktopScroll.Value += mouse.ButtonIndex == MouseButton.WheelDown ? 72 : -72; GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_toast.Visible) _toast.Position = new Vector2((Size.X - _toast.Size.X) / 2, Size.Y - 34 - _toast.Size.Y);
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus == _lastFocus) return; _lastFocus = focus;
        if (focus is null || !_desktopScroll.Visible || !IsAncestorOf(focus) || _details.IsAncestorOf(focus) || _secondary.IsAncestorOf(focus)) return;
        var rect = focus.GetGlobalRect();
        if (rect.End.Y > Size.Y) _desktopScroll.Value += rect.End.Y - Size.Y + 16;
        else if (rect.Position.Y < 0) _desktopScroll.Value += rect.Position.Y - 16;
    }

    // 设置桌面配色；按钮和视觉夹具调用同一条表现路径。
    public void SetDesktopPalette(bool blue)
    {
        MatchTheme.SetPalette(blue); Theme = MatchTheme.Create();
        _menu.RefreshPalette();
        _skillCatalog.RefreshPalette();
        _leave.RefreshPalette();
        foreach (var child in _footerActions.GetChildren().OfType<Button>()) MatchTheme.Accent(child);
        if (_view is not null) Render(_view); QueueRedraw();
    }

    private void Place(string path, float x, float y, float width, float height)
    { var cell = GetNode<Control>(path); cell.Position = new Vector2(x, y); cell.Size = new Vector2(width, height); }

    public override void _Draw()
    {
        if (Theme is null) return;
        MatchTheme.DrawBackground(this);
        DrawLine(_top.Position + new Vector2(0, _top.Size.Y + 4), _top.Position + new Vector2(_top.Size.X, _top.Size.Y + 4), new Color(MatchTheme.Gold, .25f));
        if (_page != MatchPage.HeroSelection)
        {
            // 原HTML的app伪元素先于面板绘制，装饰不得透过英雄底板。
            if (Size.X > 1150)
                foreach (var x in new[] { 29f, Size.X - 29 })
                {
                    var points = Enumerable.Range(0, 100).Select(i => new Vector2(x + 13 * Mathf.Cos(Mathf.Tau * i / 99), Size.Y / 2 + Mathf.Max(0, Size.Y - 240) / 2 * Mathf.Sin(Mathf.Tau * i / 99))).ToArray();
                    DrawPolyline(points, new Color("c6aa6330"), 1, true);
                }
            MatchTheme.DrawSurface(this, _heroRect, "hero");
            if (!_enemy.Visible) MatchTheme.DrawSurface(this, _stageRect, "stage");

            if (_navigation.Visible)
                foreach (Label label in _navigation.GetChildren())
                    if (label.GetThemeColor("font_color").IsEqualApprox(MatchTheme.Gold))
                        DrawLine(label.GlobalPosition - GlobalPosition + new Vector2(0, 38), label.GlobalPosition - GlobalPosition + new Vector2(34, 38), MatchTheme.Gold, 2);
        }
        foreach (var path in new[] { "ContextRow/ContextHost" })
        {
            var cell = GetNode<Control>(path);
            if (!cell.IsVisibleInTree()) continue;
            if (_page == MatchPage.HeroSelection)
                MatchTheme.DrawSurface(this, new Rect2(cell.GlobalPosition - GlobalPosition - Vector2.One * 6, cell.Size + Vector2.One * 12));
        }
    }
}
