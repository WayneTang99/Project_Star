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

namespace Project_Star.Presentation.Game;

// 16:9中轴桌面；敌方/阶段与己方战场上下排布，英雄HUD居中，子视图仅消费快照。
public sealed partial class MatchShell : Control
{
    public event Action? OnlineRequested;
    public event Action<MatchPage, StringName, long>? ChoiceSelected;
    public event Action<int, long>? BuyRequested;
    public event Action<BoardDragData, int, long>? GemSocketRequested;
    public event Func<BoardDragData, int, long, Result>? GemSocketPreviewRequested;
    public event Action<MatchAction>? ActionRequested;
    public event Action<BoardZone, int>? BoardSlotPressed;
    public event Action<BoardDragData, BoardZone, int>? MoveRequested;
    public event Func<BoardDragData, BoardZone, int, Result<BoardPlacementResult>>? MovePreviewRequested;
    public event Action? CancelRequested;
    public event Action<Guid, Project_Star.Domain.Common.EntityId, int, int>? SellRequested;
    public event Action<Guid, Project_Star.Domain.Common.EntityId, int, int>? DragSellRequested;
    public event Func<BoardDragData, Result<int>>? SellPreviewRequested;
    public event Action<Guid, int, long>? ClaimRequested;
    public event Action? NewJourneyRequested;
    private GameLobbyView _lobby = null!;
    private AtlasHubView _atlas = null!;
    private bool _lobbyEnabled, _atlasBrowsing;
    public bool IsLobbyNavigationActive => _lobbyEnabled && (_lobby.Visible || _atlasBrowsing);
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
    private GemShopView _gemShop = null!;
    private ResultView _result = null!;
    private BoardZoneView _battlefield = null!;
    private BoardZoneView _bench = null!;
    private BoardZoneView _enemy = null!;
    private CardDetailsView _details = null!;
    private CardPreviewView _cardPreview = null!;
    private CardPreviewQuery? _previewQuery;
    private HeroDetailsView _secondary = null!;
    private SellDropZone _sellDrop = null!;
    private MatchPage? _page;
    private int _capacity = 10;
    private VBoxContainer _footerActions = null!;
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
    private Button _benchToggle = null!;
    private Button _benchClose = null!;
    private TextureRect _tableSurface = null!;
    private TextureRect _contextAvatar = null!;
    private bool _benchOpen;
    private double _benchHoverTime;
    private double _benchExitTime;


    public override void _Ready()
    {
        // HTML 按 sRGB 合成透明层，纯界面视口使用相同的合成空间。
        GetViewport().UseHdr2D = false;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        OversamplingWithScale = OversamplingWithScaleEnum.Enabled;
        Theme = MatchTheme.Create();
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Game/WarmTable.gdshader") };
        var background = new ColorRect { Name = "Background", Color = MatchTheme.Background,
            MouseFilter = MouseFilterEnum.Ignore, ShowBehindParent = true };
        AddChild(background); MoveChild(background, 0);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _tableSurface = Add(this, new TextureRect { Name = "TableMaterial", MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, TextureRepeat = TextureRepeatEnum.Enabled });
        MoveChild(_tableSurface, 1);
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
        _heroStats = Add(this, new HBoxContainer { Name = "HeroStats" }); _heroStats.AddThemeConstantOverride("separation", 20);
        var armor = new HBoxContainer(); var regen = new HBoxContainer(); _heroStats.AddChild(armor); _heroStats.AddChild(regen);
        var armorTitle = new Label { Text = "护甲" }; MatchTheme.Text(armorTitle, 11, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.Armor)); armor.AddChild(armorTitle);
        _heroArmor = new Label(); MatchTheme.Text(_heroArmor, 15, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.Armor), true); armor.AddChild(_heroArmor);
        var regenTitle = new Label { Text = "再生" }; MatchTheme.Text(regenTitle, 11, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.HealthRegen)); regen.AddChild(regenTitle);
        armorTitle.Hide(); regenTitle.Hide();
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore }; _heroStats.AddChild(spacer); _heroStats.MoveChild(spacer, 1);
        armor.TooltipText = "护甲：优先抵扣普通伤害与灼伤伤害"; regen.TooltipText = "生命再生：每秒恢复，不超过最大生命";
        armor.FocusMode = regen.FocusMode = FocusModeEnum.All;
        _heroStats.Reparent(_hero.GetNode<ResourceBar>("HealthBar"));
        _heroRegen = new Label(); MatchTheme.Text(_heroRegen, 15, AttributePalette.Find(Project_Star.Domain.Common.GameAttributeKeys.HealthRegen), true); regen.AddChild(_heroRegen);
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
        _benchToggle = Add(this, new Button { Name = "BenchToggle", Text = "打开备战\n0 / 10", ZIndex = 4 });
        _benchToggle.AddThemeFontSizeOverride("font_size", 13);
        _benchToggle.Pressed += ToggleBench;
        _benchClose = Add(this, new Button { Name = "CloseBench", Text = "×", TooltipText = "收起备战区", ZIndex = 5 });
        _benchClose.Pressed += CloseBench;
        _contextAvatar = Add(this, new TextureRect { Name = "ContextAvatar", MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Game/PortraitClip.gdshader") } });
        _settings = Add(this, new DisplaySettingsView { Name = "DisplaySettings", ZIndex = 50 });
        _encounters = Add(host, new EncounterSelectionView { Name = "EncounterChoiceView" });
        _events = Add(host, new KeyedActionView { Name = "EventView" });
        _shop = Add(host, new ShopView { Name = "ShopView" });
        _gemShop = Add(host, new GemShopView { Name = "GemShopView" });
        _gemShop.PreviewRequested = PreviewGem;
        _gemShop.DropRequested += DropGem;
        _result = Add(host, new ResultView { Name = "ResultView" });
        _enemy = Add(host, new BoardZoneView { Name = "EnemyBoard", ShowHeading = false });
        _battleStage = Add(host, new BattleStageView { Name = "BattleStage" });
        _battleStage.Requested += RequestAction;
        _battlefield = Add(GetNode<Control>("BattlefieldRow/Content"), new BoardZoneView { Name = "Board", ShowHeading = false });
        _bench = Add(GetNode<Control>("BenchRow/Content"), new BoardZoneView { Name = "Board", ShowHeading = false });
        _details = Add(this, new CardDetailsView { Name = "CardDetails", ZIndex = 40 });
        _cardPreview = Add(this, new CardPreviewView { Name = "CardPreview", ZIndex = 55 });
        _secondary = Add(this, new HeroDetailsView { Name = "HeroDetails", ZIndex = 45 });
        _toast = Add(this, new PanelContainer { Name = "Toast", ZIndex = 40, MouseFilter = MouseFilterEnum.Ignore, Visible = false });
        var toastPlate = MatchTheme.Surface(new Color("4c4033"), MatchTheme.Gold);
        toastPlate.ContentMarginLeft = toastPlate.ContentMarginRight = 24; toastPlate.ContentMarginTop = toastPlate.ContentMarginBottom = 12;
        _toast.AddThemeStyleboxOverride("panel", toastPlate);
        _toastText = new Label(); MatchTheme.Text(_toastText, 14, new Color("eedab1")); _toast.AddChild(_toastText);
        _sellDrop = Add(this, new SellDropZone { Name = "SellDropZone", ZIndex = 20 });
        _sellDrop.PreviewRequested = PreviewSale;
        _sellDrop.Boards = new[] { _battlefield, _bench };
        _sellDrop.Gems = _gemShop;
        _sellDrop.BenchToggle = _benchToggle;
        _sellDrop.DropRequested += DropSale;
        _heroes.Selected += SelectHero; _encounters.Selected += SelectEncounter; _events.Selected += SelectEvent;
        _shop.BuyRequested += Buy; _leave.Requested += RequestAction;
        _shop.DetailsRequested += ShowDetails; _details.SellRequested += Sell; _details.Closed += CloseDetails;
        _result.ClaimRequested += Claim; _result.DetailsRequested += ShowDetails;
        _hero.SkillDetailsRequested += ShowSkill; _hero.SectionRequested += ShowSection; _secondary.ClaimRequested += Claim; _secondary.DetailsRequested += ShowDetails;
        _battlefield.SlotPressed += BattlefieldSlot; _bench.SlotPressed += BenchSlot;
        _battlefield.PreviewRequested = PreviewMove; _bench.PreviewRequested = PreviewMove;
        _battlefield.DropRequested += DropMove; _bench.DropRequested += DropMove;
        _battlefield.DetailsRequested += ShowDetails; _bench.DetailsRequested += ShowDetails; _enemy.DetailsRequested += ShowDetails;
        _lobby = Add(this, new GameLobbyView { Name = "GameLobby", ZIndex = 20 });
        _atlas = Add(this, new AtlasHubView { Name = "AtlasHub", ZIndex = 25 });
        _lobby.StartRequested += StartLobbyJourney; _lobby.ResumeRequested += ResumeJourney;
        _lobby.AtlasRequested += () => { _atlasBrowsing = true; _lobby.Hide(); _atlas.Open(); UpdateLobbyInput(); };
        _lobby.SettingsRequested += OpenSettings;
        _lobby.OnlineRequested += () => OnlineRequested?.Invoke();
        _atlas.Closed += () => { _atlasBrowsing = false; _lobby.Open(_view?.Player?.Status == MatchStatus.InProgress); _lobby.GetNode<Button>("SceneStage/Atlas").GrabFocus(); UpdateLobbyInput(); };
        _atlas.CardsRequested += OpenCatalog; _atlas.SkillsRequested += OpenSkillCatalog;
        _atlas.CardRequested += ShowDetails; _atlas.SkillRequested += ShowSkill;
        _menu.LobbyRequested += ShowLobby;
        Resized += LayoutShell; LayoutShell();
        GetTree().NodeAdded += AddButtonFeedback;
        foreach (var button in FindChildren("*", "BaseButton", true, false).OfType<BaseButton>()) AddButtonFeedback(button);
    }

    // 子组件共享同一页面捕获，只有当前中央页面可见。
    public void Render(MatchPageViewModel view)
    {
        var previousMessage = _view?.Message;
        if (_page != view.Page || _view?.Player?.MatchId != view.Player?.MatchId) _benchOpen = false;
        if (_cardPreview.Visible && (_page != view.Page || _view?.Player?.MatchId != view.Player?.MatchId)) _cardPreview.Close();
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
        _benchToggle.Disabled = view.Page is MatchPage.Preparation or MatchPage.BattlePlayback || !view.BoardEnabled;
        _benchToggle.Text = $"{(_benchOpen ? "收起备战" : "打开备战")}\n{view.Player?.BoardPlacements.Where(p => p.Zone == BoardZone.Bench).Sum(p => p.EndExclusive - p.Start) ?? 0} / {view.Player?.BenchCapacity ?? 10}";
        _benchToggle.TooltipText = _benchToggle.Disabled ? "战斗中保持双方战场可见" : "打开备战 · 拖拽停留可展开 · Escape收起";
        _contextAvatar.Texture = !view.ContextIllustration.IsEmpty && ResourceLoader.Exists(view.ContextIllustration.ToString())
            ? GD.Load<Texture2D>(view.ContextIllustration.ToString()) : null;
        _portrait.Render(view.Playback is null ? view.Title : view.Enemy?.Hero?.DisplayName ?? "敌方",
            view.EncounterLevel, view.Page is MatchPage.Preparation or MatchPage.BattlePlayback ? view.Enemy?.Hero : null,
            view.Playback?.State.Opponent);
        _portrait.SetContext(view.ContextIllustration, view.Message);
        _portrait.SetEyebrow($"第 {(view.DisplayTurn > 0 ? view.DisplayTurn : view.Player?.Turn ?? 0)} 回合 · {(view.Page is MatchPage.Shop or MatchPage.GemShop ? $"{view.ShopLevel} 级商店" : view.Page is MatchPage.Preparation or MatchPage.BattlePlayback ? "怪物遭遇" : "旅途抉择")}");
        _hero.Render(view.Player?.Hero); _progress.Render(view.Player); _leave.Render(view);
        _hero.RenderBattle(view.Playback?.State.Player, view.Playback);
        _hero.RenderSkills(view.Player?.Skills ?? Array.Empty<SkillSnapshot>(), view.Player?.Sets.Count ?? 0);
        _heroArmor.Text = (view.Playback?.State.Player.Armor ?? view.Player?.Hero?.CombatValues.GetValueOrDefault(Project_Star.Domain.Common.GameAttributeKeys.Armor) ?? 0).ToString();
        _heroRegen.Text = "+" + (view.Player?.Hero?.CombatValues.GetValueOrDefault(Project_Star.Domain.Common.GameAttributeKeys.HealthRegen) ?? 0).ToString();
        _heroes.Render(view.Message, view.Heroes);
        _lobby.Render(view.Heroes);
        _encounters.Render(view.Message, view.Choices);
        _events.Render(view.Message, view.EventOptions, view.EventRevision, view.ContextIllustration, view.EncounterLevel);
        _shop.Render(view.Message, view.Offers, view.ShopLevel);
        _gemShop.Render(view.Player, view.GemOffers);
        if (view.Page is MatchPage.BattleResult or MatchPage.MatchEnded) _result.Render(view);
        var browsing = _catalog.Visible || _skillCatalog.Visible;
        _heroes.Visible = view.Page == MatchPage.HeroSelection && !browsing && !_lobbyEnabled;
        _encounters.Visible = view.Page == MatchPage.EncounterChoice;
        _events.Visible = view.Page == MatchPage.Event;
        _shop.Visible = view.Page == MatchPage.Shop;
        _gemShop.Visible = view.Page == MatchPage.GemShop;
        _result.Visible = view.Page is MatchPage.BattleResult or MatchPage.MatchEnded;
        var capacity = Math.Max(view.Player?.BattlefieldCapacity ?? 0,
            Math.Max(view.Player?.BenchCapacity ?? 0, view.Enemy?.BattlefieldCapacity ?? 0));
        var capacityChanged = capacity > 0 && _capacity != capacity;
        if (capacity > 0) _capacity = capacity;
        _battlefield.SharedCapacity = _bench.SharedCapacity = _enemy.SharedCapacity = capacity;
        _battlefield.Render(view.Player, BoardZone.Battlefield, view.BoardEnabled, view.SelectedCardId, "战场");
        _bench.Render(view.Player, BoardZone.Bench, view.BoardEnabled, view.SelectedCardId, "备战");
        if (view.Page == MatchPage.GemShop)
            _battlefield.TooltipText = _bench.TooltipText = "拖至宝石镶嵌 · 移出棋盘出售 · Escape取消拖拽";
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
        if (_lobbyEnabled && view.Page == MatchPage.HeroSelection && _page != MatchPage.HeroSelection)
        {
            _lobby.Open(false); _heroes.Hide();
        }
        _page = view.Page;
        LayoutShell(); UpdateLobbyInput();
        if (view.Playback is not null) _progress.RenderPlayback(string.Join("\n", view.Playback.Feedback));
        var selected = view.Player?.Cards.FirstOrDefault(card => card.Id == view.SelectedCardId);
        if (selected is not null)
        {
            _secondary.Hide();
            _details.ShowSelected(DescribeCard(selected), view.Player!.MatchId, view.Sell, GetLocalMousePosition(), Size, BattleState(selected));
        }
        else if (preserveDetail && previousDetailId is { } detailId)
        {
            var refreshed = view.Player?.Cards.FirstOrDefault(card => card.Id == detailId)
                ?? view.Enemy?.Cards.FirstOrDefault(card => card.Id == detailId)
                ?? view.Offers.Select(offer => offer.Card).Concat(view.Rewards.Where(reward => reward.Card is not null).Select(reward => reward.Card!))
                    .FirstOrDefault(card => card.Id == detailId && card.Key == _details.CurrentCardKey);
            if (refreshed is not null)
                _details.RefreshCard(DescribeCard(refreshed), Size, view.Playback?.State.Cards.FirstOrDefault(state => state.Id == detailId));
            else
            {
                _details.Hide();
                _pinnedDetails = false;
            }
        }
    }

    public override void _ExitTree()
    {
        _gemShop.PreviewRequested = null; _gemShop.DropRequested -= DropGem;
        GetTree().NodeAdded -= AddButtonFeedback;
        _desktopScroll.ValueChanged -= ScrollChanged;
        _benchToggle.Pressed -= ToggleBench; _benchClose.Pressed -= CloseBench;
        _toastTween?.Kill();
        _battleStage.Requested -= RequestAction;
        Resized -= LayoutShell;
        _catalog.Closed -= CloseCatalog; _skillCatalog.Closed -= CloseSkillCatalog;
        _menuButton.Pressed -= OpenMenu;
        _menu.SettingsRequested -= OpenSettings; _menu.CardsRequested -= OpenCatalog; _menu.SkillsRequested -= OpenSkillCatalog;
        _menu.AbandonRequested -= Abandon;
        _heroes.Selected -= SelectHero; _encounters.Selected -= SelectEncounter; _events.Selected -= SelectEvent;
        _shop.BuyRequested -= Buy; _leave.Requested -= RequestAction;
        _shop.DetailsRequested -= ShowDetails; _details.SellRequested -= Sell; _details.Closed -= CloseDetails;
        _result.ClaimRequested -= Claim; _result.DetailsRequested -= ShowDetails;
        _hero.SkillDetailsRequested -= ShowSkill; _hero.SectionRequested -= ShowSection; _secondary.ClaimRequested -= Claim; _secondary.DetailsRequested -= ShowDetails;
        _battlefield.SlotPressed -= BattlefieldSlot; _bench.SlotPressed -= BenchSlot;
        _battlefield.PreviewRequested = null; _bench.PreviewRequested = null;
        _battlefield.DropRequested -= DropMove; _bench.DropRequested -= DropMove;
        _battlefield.DetailsRequested -= ShowDetails; _bench.DetailsRequested -= ShowDetails; _enemy.DetailsRequested -= ShowDetails;
        _sellDrop.PreviewRequested = null;
        _sellDrop.DropRequested -= DropSale;
    }

    private void ToggleBench() => SetBenchOpen(!_benchOpen);
    private void CloseBench() => SetBenchOpen(false);
    // 备战展开只改变视图；战斗时始终保留上方敌方战场。
    internal void SetBenchOpen(bool open)
    {
        if (open && (_benchToggle.Disabled || _view?.Player is null)) return;
        _benchOpen = open; _benchExitTime = 0; _benchHoverTime = 0;
        _benchToggle.Text = (_benchOpen ? "收起备战" : "打开备战") + "\n" + _benchToggle.Text.Split('\n').Last();
        _details.Hide(); _pinnedDetails = false;
        LayoutShell();
        if (!open) _benchToggle.GrabFocus();
    }

    private void SelectHero(StringName key, long revision)
    {
        _lobby.Hide(); UpdateLobbyInput();
        ChoiceSelected?.Invoke(MatchPage.HeroSelection, key, revision);
    }
    public void EnableLobby()
    {
        _lobbyEnabled = true;
        _menu.GetNode<Button>("ItemsPanel/Items/OpenCardCatalog").Hide();
        _menu.GetNode<Button>("ItemsPanel/Items/OpenSkillCatalog").Hide();
        ShowLobby();
    }
    public void SetWorldCatalog(IReadOnlyList<WorldCatalogEntry> entries) => _atlas.SetEntries(entries);
    public void ShowLobby()
    {
        _benchOpen = false; _toast.Hide(); _toastTween?.Kill();
        _atlasBrowsing = false; _atlas.Hide(); _catalog.Hide(); _skillCatalog.Hide();
        _menu.Close(); _details.Hide(); _secondary.Hide(); _cardPreview.Close(); _settings.Hide();
        _heroes.Hide(); _lobby.Open(_view?.Player?.Status == MatchStatus.InProgress);
        UpdateLobbyInput();
    }
    private void StartLobbyJourney()
    {
        var key = _lobby.SelectedHeroKey;
        if (!_lobby.Visible || _view?.Heroes.Any(hero => hero.Key == key && hero.Action.Visible && hero.Action.Enabled) != true) return;
        if (_view.Player is not null) NewJourneyRequested?.Invoke();
        SelectHero(key, 0);
    }
    private void ResumeJourney()
    { _lobby.Hide(); UpdateLobbyInput(); if (_view is not null) Render(_view); _menuButton.GrabFocus(); }
    private void UpdateLobbyInput()
    {
        if (!_lobbyEnabled) return;
        var blocked = _lobby.Visible || _atlasBrowsing;
        foreach (var child in GetChildren().OfType<Control>())
        {
            if (child == _lobby || child == _atlas || child == _catalog || child == _skillCatalog || child == _settings
                || child == _cardPreview || child == _details || child == _secondary) continue;
            child.ProcessMode = blocked ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;
        }
        _menuButton.Visible = !_lobby.Visible && !_atlasBrowsing;
    }
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
    private void OpenSettings()
    {
        if (_catalog.Visible || _skillCatalog.Visible) return;
        // Godot鼠标命中按场景树顺序；仅提高ZIndex无法越过后创建的大厅。
        MoveChild(_settings, GetChildCount() - 1);
        _settings.Open();
    }
    // 入口在真正启动游戏时载入显示配置，验证夹具不修改用户窗口设置。
    public void LoadDisplaySettings() => _settings.LoadSaved();
    // 图鉴内容由入口的应用查询注入，不读取对局状态。
    public void SetCatalog(IReadOnlyList<CardCatalogEntry> entries) { _catalog.SetEntries(entries); _details.SetEntries(entries); _cardPreview.SetEntries(entries); }

    // 原生视图共享只读等级／宝石查询，卡面补充当前套装展示信息。
    public void SetCardPreview(CardPreviewQuery query) { _previewQuery = query; _cardPreview.SetQuery(query); }
    public CardSnapshot DescribeCard(CardSnapshot card) => _previewQuery?.Describe(card,
        _catalog?.Visible == true || _atlasBrowsing ? null
            : _view?.Enemy?.Cards.Any(item => item.Id == card.Id && item.Key == card.Key) == true ? _view.Enemy : _view?.Player) ?? card;
    private void ShowSkill(SkillSnapshot skill) { _details.Hide(); _secondary.ShowSkill(skill, Size); }

    // 技能图鉴同样只接收应用查询的冻结条目。
    public void SetSkillCatalog(IReadOnlyList<SkillCatalogEntry> entries) => _skillCatalog.SetEntries(entries);
    private void OpenCatalog()
    {
        _menu.Close(); _details.Hide(); _secondary.Hide(); _heroes.Hide();
        _settings.Hide(); _skillCatalog.Hide(); _catalog.Open();
    }
    private void CloseCatalog()
    {
        if (_atlasBrowsing) { _atlas.ReturnFromCatalog(new StringName("cards")); UpdateLobbyInput(); return; }
        _heroes.Visible = !_lobbyEnabled && _page == MatchPage.HeroSelection; _menuButton.GrabFocus();
    }
    private void OpenSkillCatalog()
    {
        _menu.Close(); _details.Hide(); _secondary.Hide(); _heroes.Hide();
        _settings.Hide(); _catalog.Hide(); _skillCatalog.Open();
    }
    private void CloseSkillCatalog()
    {
        if (_atlasBrowsing) { _atlas.ReturnFromCatalog(new StringName("skills")); UpdateLobbyInput(); return; }
        _heroes.Visible = !_lobbyEnabled && _page == MatchPage.HeroSelection; _menuButton.GrabFocus();
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
    {
        _details.Hide();
        var opponent = _view?.Enemy?.Cards.Any(item => item.Id == card.Id && item.Key == card.Key) == true;
        _cardPreview.Open(_catalog.Visible || _atlasBrowsing ? card : DescribeCard(card), _catalog.Visible || _atlasBrowsing ? null : opponent ? _view?.Enemy : _view?.Player, _catalog.Visible || _atlasBrowsing ? null : opponent ? _view?.Player : _view?.Enemy, rect);
    }
    // 图鉴卡牌与棋盘共用右键预览入口。
    public void ShowCardPreview(CardSnapshot card, Rect2 rect) => ShowDetails(card, rect);
    private void CloseDetails() { _pinnedDetails = false; CancelRequested?.Invoke(); }
    // 卡牌悬停立即复用固定详情的内容组件，定位依据卡牌可见边界。
    public void ShowHoverCard(CardSnapshot card, Rect2 rect, CardBattleSnapshot? battle = null, string interactionHint = "")
    { if (!_pinnedDetails && !_cardPreview.Visible && !_menu.Visible && !_settings.Visible && !_skillCatalog.Visible && !GetViewport().GuiIsDragging()) { _details.SetPinned(false); _details.ShowNear(DescribeCard(card), new Rect2(rect.Position - GlobalPosition, rect.Size), Size, _catalog.Visible ? null : battle ?? BattleState(card)); _details.SetInteractionHint(interactionHint); } }
    // 固定详情不随卡牌离开而关闭。
    public void HideHoverCard(bool dragging = false)
    {
        if (dragging) { _pinnedDetails = false; _details.SetPinned(false); }
        if (!_pinnedDetails) _details.Hide();
    }
    private void DropMove(BoardDragData data, BoardZone zone, int start) => MoveRequested?.Invoke(data, zone, start);
    private void DropSale(BoardDragData data, CardSnapshot card) =>
        DragSellRequested?.Invoke(data.MatchId, data.CardId, card.Level, card.Value);
    private void DropGem(BoardDragData data, int index, long revision) => GemSocketRequested?.Invoke(data, index, revision);
    private Result PreviewGem(BoardDragData data, int index, long revision) => GemSocketPreviewRequested?.Invoke(data, index, revision)
        ?? Result.Fail(new Failure(new StringName("ui.gem_unavailable"), "镶嵌暂不可用。"));
    private Result<int> PreviewSale(BoardDragData data) => SellPreviewRequested?.Invoke(data)
        ?? Result<int>.Fail(new Failure(new StringName("ui.sale_unavailable"), "当前不可出售。"));
    private Result<BoardPlacementResult> PreviewMove(BoardDragData data, BoardZone zone, int start) =>
        MovePreviewRequested?.Invoke(data, zone, start) ?? Result<BoardPlacementResult>.Fail(
            new Failure(new StringName("ui.preview_unavailable"), "当前区域不可移动。"));

    public override void _UnhandledKeyInput(InputEvent input)
    {
        if (_lobbyEnabled && IsLobbyNavigationActive)
        {
            return;
        }
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape } && _benchOpen)
        { CloseBench(); GetViewport().SetInputAsHandled(); return; }
        if (input is InputEventKey { Pressed: true, Keycode: Key.Escape })
        { _pinnedDetails = false; _details.Hide(); _secondary.Hide(); CancelRequested?.Invoke(); GetViewport().SetInputAsHandled(); }
    }

    private static T Add<T>(Control parent, T child) where T : Control { parent.AddChild(child); return child; }

    private void AddButtonFeedback(Node node)
    {
        if (node is not BaseButton button || !IsAncestorOf(button)) return;
        // 营地角色已有独立的脚下选中圈，避免通用矩形反馈覆盖透明插画。
        if (button.GetParent()?.GetParent() is GameLobbyView) return;
        // NodeAdded期间节点仍在进入树，延后挂载装饰以免修改中的树重入。
        Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree() || !IsAncestorOf(button)
                || button.GetNodeOrNull<ButtonFeedback>("InteractionFeedback") is not null) return;
            if (button.GetParent()?.GetParent() is GameLobbyView) return;
            button.AddChild(new ButtonFeedback { Name = "InteractionFeedback" });
        }).CallDeferred();
    }

    private void LayoutShell()
    {
        if (_layingOut || _desktopScroll is null) return;
        _layingOut = true;
        const float margin = 32;
        var choosing = _page == MatchPage.HeroSelection;
        var width = Size.X - margin * 2;
        var center = Size.X / 2;
        var battle = _battleStage.Visible;
        _documentHeight = Size.Y;
        _desktopScroll.MaxValue = _desktopScroll.Page = Size.Y;
        _desktopScroll.Value = 0; _desktopScroll.Hide();
        _tableSurface.Position = new Vector2(center - 675, 130); _tableSurface.Size = new Vector2(1350, 720);
        _tableSurface.Visible = !choosing; _tableSurface.Texture = MatchTheme.Skin.TableSurface;
        _tableSurface.Material = MatchTheme.Skin.SurfaceEffect;
        _top.Visible = true;
        _top.Position = new Vector2(margin, 0); _top.Size = new Vector2(width, 64);
        _stageRect = new Rect2(center - 560, 250, 1120, 230);
        _heroRect = new Rect2(center - 600, 750, 1200, 124);
        foreach (var path in new[] { "ContextRow", "BattlefieldRow", "BenchRow" })
        {
            var row = GetNode<Control>(path); row.Position = Vector2.Zero; row.Size = Size;
            row.MouseFilter = MouseFilterEnum.Ignore;
            foreach (Control cell in row.GetChildren()) cell.MouseFilter = MouseFilterEnum.Ignore;
        }
        Place("ContextRow/Portrait", _stageRect.Position.X + 1, 251, 209, 228);
        _portrait.Size = GetNode<Control>("ContextRow/Portrait").Size;
        Place("ContextRow/Leave", _stageRect.Position.X + 24, 420, 170, 44);
        _leaveScroll.Size = GetNode<Control>("ContextRow/Leave").Size;
        var hostLeft = battle ? _stageRect.Position.X : _stageRect.Position.X + 210;
        Place("ContextRow/ContextHost", hostLeft, 250, battle ? 1120 : 910, 230);
        Place("BattlefieldRow/Content", center - 523, 500, 1046, 230);
        Place("BenchRow/Content", center - 523, 250, 1046, 230);
        foreach (var view in new Control[] { _heroes, _encounters, _events, _shop, _gemShop, _result })
        { view.Position = Vector2.Zero; view.Size = GetNode<Control>("ContextRow/ContextHost").Size; }
        _enemy.Position = new Vector2(37, 0); _enemy.Size = new Vector2(1046, 230);
        _battleStage.Position = new Vector2(0, -140); _battleStage.Size = new Vector2(1120, 130);
        _battlefield.Size = _bench.Size = new Vector2(1046, 230);
        Place("BenchRow/Hero", center - 600, 750, 1200, 124);
        _hero.Size = GetNode<Control>("BenchRow/Hero").Size;
        Place("BenchRow/Progress", center - 440, 800, 244, 36);
        _progress.Size = GetNode<Control>("BenchRow/Progress").Size;
        _heroStats.Position = new Vector2(7, 0); _heroStats.Size = new Vector2(230, 24);
        _footerActions.Position = new Vector2(center + 592, 462); _footerActions.Size = new Vector2(160, 56); _footerActions.ZIndex = 7;
        _bottomLine.Hide();
        GetNode<Control>("BattlefieldRow").Visible = GetNode<Control>("BenchRow").Visible = !choosing;
        GetNode<Control>("BenchRow/Content").Visible = _benchOpen && !choosing;
        GetNode<Control>("ContextRow/ContextHost").Visible = !_benchOpen;
        GetNode<Control>("ContextRow/Portrait").Visible = !choosing && !_benchOpen && !battle;
        GetNode<Control>("ContextRow/Leave").Visible = !choosing && !_benchOpen && !battle;
        _footerActions.Visible = _heroStats.Visible = !choosing;
        _benchToggle.Visible = !choosing; _benchClose.Visible = !choosing && _benchOpen;
        _benchToggle.Position = new Vector2(center + 484, 760); _benchToggle.Size = new Vector2(100, 74);
        var dock = MatchTheme.Plate("dock", 6);
        _benchToggle.AddThemeStyleboxOverride("normal", dock);
        _benchClose.Position = new Vector2(center + 495, 216); _benchClose.Size = new Vector2(28, 28);
        _contextAvatar.Position = new Vector2(center - 45, 110); _contextAvatar.Size = new Vector2(90, 90);
        _contextAvatar.Visible = !choosing && !battle && _contextAvatar.Texture is not null;
        if (choosing)
        {
            var host = GetNode<Control>("ContextRow/ContextHost"); host.Position = new Vector2(margin, 90);
            host.Size = new Vector2(width, Size.Y - 108); _heroes.Size = host.Size;
        }
        _menuButton.Position = new Vector2(Size.X - margin - 96, 12); _menuButton.Size = new Vector2(96, 40);
        _menu.Position = Vector2.Zero; _menu.Size = Size;
        _economy.Position = new Vector2(center - 584, 760); _economy.Size = new Vector2(100, 74);
        _lobby.Position = _atlas.Position = Vector2.Zero; _lobby.Size = _atlas.Size = Size;
        _settings.Position = Vector2.Zero; _settings.Size = Size;
        _catalog.Position = Vector2.Zero; _catalog.Size = Size;
        _cardPreview.Position = Vector2.Zero; _cardPreview.SetBounds(Size);
        _skillCatalog.Position = _catalog.Position; _skillCatalog.Size = _catalog.Size;
        _details.ClampTo(Size); _secondary.ClampTo(Size);
        _sellDrop.Position = Vector2.Zero; _sellDrop.Size = Size;
        _portrait.Visible = !choosing && !battle && !_benchOpen;
        _layingOut = false; QueueRedraw();
    }

    private void ScrollChanged(double value) { if (!_pinnedDetails) _details.Hide(); LayoutShell(); }
    // 固定16:9画布隐藏整页滚动；浮层和图鉴自行消费滚轮。
    public override void _Input(InputEvent input)
    {
        if (_lobbyEnabled && _lobby.Visible && !_atlasBrowsing && !_settings.Visible && input is InputEventKey { Pressed: true } lobbyKey)
        { MatchMenuView.TrapTab(_lobby, lobbyKey); return; }
        if (_cardPreview.Visible) return;
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
        if (GetViewport().GuiIsDragging() && !_benchToggle.Disabled && !_benchOpen && _benchToggle.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
        { _benchHoverTime += delta; if (_benchHoverTime >= .35) SetBenchOpen(true); }
        else _benchHoverTime = 0;
        if (_benchOpen && GetViewport().GuiIsDragging() && !_bench.GetGlobalRect().Grow(24 * GetGlobalTransform().Scale.X).HasPoint(GetGlobalMousePosition())
            && !_benchToggle.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
        { _benchExitTime += delta; if (_benchExitTime >= .15) SetBenchOpen(false); }
        else _benchExitTime = 0;
        var focus = GetViewport().GuiGetFocusOwner();
        if (focus == _lastFocus) return; _lastFocus = focus;
        if (focus is null || !_desktopScroll.Visible || !IsAncestorOf(focus) || _details.IsAncestorOf(focus) || _secondary.IsAncestorOf(focus)) return;
        var rect = focus.GetGlobalRect();
        if (rect.End.Y > Size.Y) _desktopScroll.Value += rect.End.Y - Size.Y + 16;
        else if (rect.Position.Y < 0) _desktopScroll.Value += rect.Position.Y - 16;
    }

    // 皮肤资源仅改变材质与装饰，布局和输入区域继续使用同一组矩形。
    public void ApplyBoardSkin(BoardSkin skin)
    {
        MatchTheme.SetSkin(skin); LayoutShell(); QueueRedraw();
        _hero.QueueRedraw(); _battleStage.QueueRedraw();
    }

    private void Place(string path, float x, float y, float width, float height)
    { var cell = GetNode<Control>(path); cell.Position = new Vector2(x, y); cell.Size = new Vector2(width, height); }

    public override void _Draw()
    {
        if (Theme is null) return;
        MatchTheme.DrawBackground(this);
        if (_page != MatchPage.HeroSelection) MatchTheme.DrawTable(this);
        DrawLine(new Vector2(_top.Position.X, 100), new Vector2(_top.Position.X + _top.Size.X, 100), new Color(MatchTheme.Gold, .25f));
        if (_page != MatchPage.HeroSelection)
        {
            if (!_enemy.Visible && !_benchOpen) MatchTheme.DrawRecess(this, _stageRect);
            if (_contextAvatar.Visible) MatchTheme.DrawPortraitMount(this, _contextAvatar.GetRect(), true);

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
