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

// 中央阶段、双棋盘与底部英雄栏；子视图只展示快照并分发操作意图。
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
    private Button _catalogButton = null!;
    private SkillCatalogView _skillCatalog = null!;
    private Button _skillCatalogButton = null!;
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
    private bool _enemyExpanded;
    private Button _enemyBack = null!;
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
            { Name = "LeaveScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
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
        var armorTitle = new Label { Text = "护甲" }; MatchTheme.Text(armorTitle, 11, MatchTheme.Muted); armor.AddChild(armorTitle);
        _heroArmor = new Label(); MatchTheme.Text(_heroArmor, 17, MatchTheme.Gold, true); armor.AddChild(_heroArmor);
        var regenTitle = new Label { Text = "再生" }; MatchTheme.Text(regenTitle, 11, MatchTheme.Muted); regen.AddChild(regenTitle);
        _heroRegen = new Label(); MatchTheme.Text(_heroRegen, 17, MatchTheme.Gold, true); regen.AddChild(_heroRegen);
        _navigation = Add(this, new HBoxContainer { Name = "StageNavigation", MouseFilter = MouseFilterEnum.Ignore });
        _navigation.AddThemeConstantOverride("separation", 24);
        foreach (var title in new[] { "商店", "遭遇", "战斗" })
        {
            var label = new Label { Text = title, CustomMinimumSize = new Vector2(36, 39), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
            MatchTheme.Text(label, 14, MatchTheme.Muted, spacing: 2); _navigation.AddChild(label);
        }
        var host = GetNode<Control>("ContextRow/ContextHost");
        _heroes = Add(host, new HeroSelectionView { Name = "HeroSelectionView" });
        _catalogButton = Add(this, new Button { Name = "OpenCardCatalog", Text = "卡牌图鉴", ZIndex = 5 });
        _catalog = Add(this, new CardCatalogView { Name = "CardCatalog", ZIndex = 30 });
        _catalogButton.Pressed += OpenCatalog; _catalog.Closed += CloseCatalog;
        _skillCatalogButton = Add(this, new Button { Name = "OpenSkillCatalog", Text = "技能图鉴", ZIndex = 5 });
        _skillCatalog = Add(this, new SkillCatalogView { Name = "SkillCatalog", ZIndex = 30 });
        _skillCatalogButton.Pressed += OpenSkillCatalog; _skillCatalog.Closed += CloseSkillCatalog;
        _encounters = Add(host, new EncounterSelectionView { Name = "EncounterChoiceView" });
        _events = Add(host, new KeyedActionView { Name = "EventView" });
        _shop = Add(host, new ShopView { Name = "ShopView" });
        _result = Add(host, new ResultView { Name = "ResultView" });
        _enemy = Add(host, new BoardZoneView { Name = "EnemyBoard" });
        _enemyBack = Add(host, new Button { Name = "EnemyBack", Text = "返回战斗摘要", Visible = false, ZIndex = 4 });
        _enemyBack.AddThemeFontSizeOverride("font_size", 11); _enemyBack.AddThemeColorOverride("font_color", MatchTheme.Gold); _enemyBack.Pressed += ToggleEnemy;
        _battleStage = Add(host, new BattleStageView { Name = "BattleStage" });
        _battleStage.Requested += RequestAction;
        _battleStage.InspectRequested += ToggleEnemy;
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
    }

    // 子组件共享同一页面捕获，只有当前中央页面可见。
    public void Render(MatchPageViewModel view)
    {
        var previousMessage = _view?.Message;
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
        _hero.RenderBattle(view.Playback?.State.Player);
        _hero.RenderSkills(view.Player?.Skills ?? Array.Empty<SkillSnapshot>(), view.Player?.Sets.Count ?? 0);
        _heroArmor.Text = (view.Playback?.State.Player.Armor ?? view.Player?.Hero?.CombatValues.GetValueOrDefault(Project_Star.Domain.Common.GameAttributeKeys.Armor) ?? 0).ToString();
        _heroRegen.Text = (view.Player?.Hero?.CombatValues.GetValueOrDefault(Project_Star.Domain.Common.GameAttributeKeys.HealthRegen) ?? 0).ToString();
        _heroes.Render(view.Message, view.Heroes);
        _encounters.Render(view.Message, view.Choices);
        _events.Render(view.Message, view.EventOptions, view.EventRevision, view.ContextIllustration);
        _shop.Render(view.Message, view.Offers, view.ShopLevel);
        if (view.Page is MatchPage.BattleResult or MatchPage.MatchEnded) _result.Render(view);
        var browsing = _catalog.Visible || _skillCatalog.Visible;
        _heroes.Visible = view.Page == MatchPage.HeroSelection && !browsing;
        _catalogButton.Visible = _skillCatalogButton.Visible = view.Page == MatchPage.HeroSelection && !browsing;
        if (view.Page != MatchPage.HeroSelection) { _catalog.Hide(); _skillCatalog.Hide(); }
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
        if (_page != view.Page) _enemyExpanded = false;
        _enemy.Render(view.EnemyVisible && view.Page is MatchPage.Preparation or MatchPage.BattlePlayback ? view.Enemy : null,
            BoardZone.Battlefield, false, null, view.Playback is null ? "敌方战场"
                : $"战斗 {view.Playback.Tick / 10m:0.0}s{(view.Playback.Paused ? " · 暂停" : "")}{(view.Playback.State.Eclipse ? " · 日蚀" : "")}");
        _battleStage.Visible = view.Page is MatchPage.Preparation or MatchPage.BattlePlayback;
        if (_battleStage.Visible) _battleStage.Render(view);
        _enemy.Visible = _battleStage.Visible && _enemyExpanded;
        _battleStage.Visible &= !_enemyExpanded;
        _enemyBack.Visible = _enemy.Visible;
        if (_enemyBack.Visible) _enemyBack.Position = new Vector2(_enemy.Size.X - 138, 10);
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
                ?? view.Enemy?.Cards.FirstOrDefault(card => card.Id == detailId);
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
        _desktopScroll.ValueChanged -= ScrollChanged;
        _toastTween?.Kill();
        _battleStage.Requested -= RequestAction; _battleStage.InspectRequested -= ToggleEnemy;
        _enemyBack.Pressed -= ToggleEnemy;
        Resized -= LayoutShell;
        _catalogButton.Pressed -= OpenCatalog; _catalog.Closed -= CloseCatalog;
        _skillCatalogButton.Pressed -= OpenSkillCatalog; _skillCatalog.Closed -= CloseSkillCatalog;
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
    private void ToggleEnemy() { _enemyExpanded = !_enemyExpanded; _enemy.Visible = _enemyExpanded; _battleStage.Visible = !_enemyExpanded; _enemyBack.Visible = _enemyExpanded; }
    // 图鉴内容由入口的应用查询注入，不读取对局状态。
    public void SetCatalog(IReadOnlyList<CardCatalogEntry> entries) => _catalog.SetEntries(entries);
    // 技能图鉴同样只接收应用查询的冻结条目。
    public void SetSkillCatalog(IReadOnlyList<SkillCatalogEntry> entries) => _skillCatalog.SetEntries(entries);
    private void OpenCatalog()
    {
        if (_page != MatchPage.HeroSelection) return;
        _details.Hide(); _secondary.Hide(); _heroes.Hide(); _catalogButton.Hide(); _skillCatalogButton.Hide();
        _skillCatalog.Hide(); _catalog.Open();
    }
    private void CloseCatalog()
    {
        if (_page != MatchPage.HeroSelection) return;
        _heroes.Show(); _catalogButton.Show(); _skillCatalogButton.Show(); _catalogButton.GrabFocus();
    }
    private void OpenSkillCatalog()
    {
        if (_page != MatchPage.HeroSelection) return;
        _details.Hide(); _secondary.Hide(); _heroes.Hide(); _catalogButton.Hide(); _skillCatalogButton.Hide();
        _catalog.Hide(); _skillCatalog.Open();
    }
    private void CloseSkillCatalog()
    {
        if (_page != MatchPage.HeroSelection) return;
        _heroes.Show(); _catalogButton.Show(); _skillCatalogButton.Show(); _skillCatalogButton.GrabFocus();
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
    private void ShowDetails(CardSnapshot card) { _pinnedDetails = true; _details.ShowCard(card, GetLocalMousePosition(), Size, BattleState(card)); }
    private void CloseDetails() { _pinnedDetails = false; CancelRequested?.Invoke(); }
    // 卡牌悬停立即复用固定详情的内容组件，定位依据卡牌可见边界。
    public void ShowHoverCard(CardSnapshot card, Rect2 rect, CardBattleSnapshot? battle = null)
    { if (!_pinnedDetails && !_secondary.Visible) _details.ShowNear(card, new Rect2(rect.Position - GlobalPosition, rect.Size), Size, battle ?? BattleState(card)); }
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

    private void LayoutShell()
    {
        if (_layingOut || _desktopScroll is null) return;
        _layingOut = true;
        const float margin = 18;
        var padding = Size.X <= 900 ? 14 : Size.X <= 1150 ? 30 : 64;
        var width = Mathf.Max(1, Mathf.Min(1400, Size.X) - padding * 2);
        var left = (Size.X - width) / 2;
        var topBarY = Size.X >= 1500 ? 10 : 0;
        var topBarHeight = Size.X <= 900 ? 68 : 82;
        var top = topBarY + topBarHeight + 54;
        var side = Size.X >= 1500 ? 230 : Size.X <= 900 ? 140 : Size.X <= 1150 ? 165 : 210;
        var phaseHeight = Size.X <= 900 ? 214 : Mathf.Clamp(Size.Y * .27f, 214, 290);
        var gap = Size.X <= 900 ? 4 : 6;
        var inset = Size.X <= 900 ? 8 : 10;
        var unit = (width - inset * 2 - (_capacity - 1) * gap) / _capacity;
        var boardHeight = 27 + inset * 2 + unit * 2;
        var heroTop = top + phaseHeight + 17 + boardHeight * 2 + 17 + 18;
        const float heroHeight = 145;
        var choosing = _page == MatchPage.HeroSelection;
        _documentHeight = choosing ? Size.Y : heroTop + heroHeight + 15 + 22 + 22;
        _desktopScroll.Position = new Vector2(Size.X - 10, 0); _desktopScroll.Size = new Vector2(10, Size.Y);
        _desktopScroll.MaxValue = _documentHeight; _desktopScroll.Page = Size.Y;
        _desktopScroll.Visible = _documentHeight > Size.Y;
        var offset = choosing ? 0 : (float)_desktopScroll.Value;
        _stageRect = new Rect2(left, top - offset, width, phaseHeight);
        _heroRect = new Rect2(left, heroTop - offset, width, heroHeight);
        _top.Position = new Vector2(left, topBarY - offset); _top.Size = new Vector2(width, topBarHeight);
        _navigation.Position = new Vector2(left, topBarY + topBarHeight + 7.5f - offset); _navigation.Size = new Vector2(width, 39);
        foreach (var path in new[] { "ContextRow", "BattlefieldRow", "BenchRow" })
        {
            var row = GetNode<Control>(path); row.Position = Vector2.Zero; row.Size = new Vector2(Size.X, _documentHeight);
            row.MouseFilter = MouseFilterEnum.Ignore;
            foreach (Control cell in row.GetChildren()) cell.MouseFilter = MouseFilterEnum.Ignore;
        }
        var rightWidth = Size.X <= 900 ? 140 : Size.X <= 1150 ? 175 : 214;
        var mainLeft = Size.X <= 900 ? 100 : Size.X <= 1150 ? 129 : 154;
        var mainWidth = width - mainLeft - rightWidth - 39;
        Place("BenchRow/Hero", left + 23, heroTop + 11 - offset, width - rightWidth - 62, 123);
        Place("BenchRow/Progress", left + mainLeft, heroTop + 118 - offset, mainWidth, 16);
        _hero.Size = GetNode<Control>("BenchRow/Hero").Size; _progress.Size = GetNode<Control>("BenchRow/Progress").Size;
        _heroStats.Position = new Vector2(left + width - rightWidth + 3, heroTop + 37.5f - offset); _heroStats.Size = new Vector2(rightWidth - 20, 22);
        _footerActions.Position = new Vector2(left + width - rightWidth + 3, heroTop + 72.5f - offset); _footerActions.Size = new Vector2(rightWidth - 20, 36);
        _bottomLine.Position = new Vector2(left, heroTop + heroHeight + 15 - offset); _bottomLine.Size = new Vector2(width, 22);
        Place("ContextRow/Portrait", left + 1, top + 1 - offset, side, phaseHeight - 2);
        _portrait.Size = GetNode<Control>("ContextRow/Portrait").Size;
        Place("ContextRow/Leave", left + 25, top + phaseHeight / 2 + 48 - offset, side - 49, 55);
        _leaveScroll.Size = GetNode<Control>("ContextRow/Leave").Size;
        Place("ContextRow/ContextHost", left + side + 1, top + 1 - offset, width - side - 2, phaseHeight - 2);
        Place("BattlefieldRow/Content", left, top + phaseHeight + 17 - offset, width, boardHeight);
        Place("BenchRow/Content", left, top + phaseHeight + 34 + boardHeight - offset, width, boardHeight);
        foreach (var view in new Control[] { _heroes, _encounters, _events, _shop, _result, _enemy, _battleStage }) view.Size = GetNode<Control>("ContextRow/ContextHost").Size;
        _enemyBack.Size = new Vector2(120, 28);
        _battlefield.Size = _bench.Size = new Vector2(width, boardHeight);
        GetNode<Control>("BattlefieldRow").Visible = GetNode<Control>("BenchRow").Visible = !choosing;
        GetNode<Control>("ContextRow/Portrait").Visible = GetNode<Control>("ContextRow/Leave").Visible = !choosing;
        _navigation.Visible = _footerActions.Visible = _bottomLine.Visible = _heroStats.Visible = !choosing;
        if (choosing)
        {
            _top.Position = new Vector2(margin, 0); _top.Size = new Vector2(Size.X - margin * 2, 82);
            var host = GetNode<Control>("ContextRow/ContextHost"); host.Position = new Vector2(margin, 90); host.Size = new Vector2(Size.X - margin * 2, Size.Y - 108); _heroes.Size = host.Size;
        }
        _catalogButton.Position = new Vector2(Size.X - margin - 328, 18); _catalogButton.Size = new Vector2(160, 40);
        _skillCatalogButton.Position = new Vector2(Size.X - margin - 160, 18); _skillCatalogButton.Size = new Vector2(160, 40);
        _catalog.Position = new Vector2(margin, margin); _catalog.Size = Size - Vector2.One * margin * 2;
        _skillCatalog.Position = _catalog.Position; _skillCatalog.Size = _catalog.Size;
        _details.ClampTo(Size); _secondary.ClampTo(Size);
        _sellDrop.Position = Vector2.Zero; _sellDrop.Size = new Vector2(Size.X, Mathf.Max(0, top + phaseHeight - offset));
        _layingOut = false; QueueRedraw();
    }

    private void ScrollChanged(double value) { if (!_pinnedDetails) _details.Hide(); LayoutShell(); }
    // 原生纵向滚动保留 HTML 自然高度；浮层和图鉴自行消费滚轮。
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
            MatchTheme.DrawSurface(this, _heroRect, "hero");
            MatchTheme.DrawSurface(this, _stageRect, "stage");
            DrawLine(new Vector2(_footerActions.Position.X - 20, _heroStats.Position.Y), new Vector2(_footerActions.Position.X - 20, _footerActions.Position.Y + 36), new Color("93805a44"));
            if (_navigation.Visible)
                foreach (Label label in _navigation.GetChildren())
                    if (label.GetThemeColor("font_color").IsEqualApprox(MatchTheme.Gold))
                        DrawLine(label.GlobalPosition - GlobalPosition + new Vector2(0, 38), label.GlobalPosition - GlobalPosition + new Vector2(34, 38), MatchTheme.Gold, 2);
            if (Size.X > 1150)
                foreach (var x in new[] { _stageRect.Position.X - 35, _stageRect.End.X + 35 })
                {
                    var points = Enumerable.Range(0, 100).Select(i => new Vector2(x + 13 * Mathf.Cos(Mathf.Tau * i / 99), (120 + _heroRect.End.Y - 120) / 2 + (_heroRect.End.Y - 240) / 2 * Mathf.Sin(Mathf.Tau * i / 99))).ToArray();
                    DrawPolyline(points, new Color("c6aa6330"), 1, true);
                }
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
