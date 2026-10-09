using System;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using System.Collections.Generic;
using Project_Star.Application.Content;
using Project_Star.Domain.Match;

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

    public override void _Ready()
    {
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
        _navigation = Add(this, new HBoxContainer { Name = "StageNavigation", MouseFilter = MouseFilterEnum.Ignore });
        _navigation.AddThemeConstantOverride("separation", 28);
        foreach (var title in new[] { "遭遇", "商店", "事件", "战斗" })
            _navigation.AddChild(new Label { Text = title, MouseFilter = MouseFilterEnum.Ignore });
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
        _battlefield = Add(GetNode<Control>("BattlefieldRow/Content"), new BoardZoneView { Name = "Board" });
        _bench = Add(GetNode<Control>("BenchRow/Content"), new BoardZoneView { Name = "Board" });
        _details = Add(this, new CardDetailsView { Name = "CardDetails", ZIndex = 10 });
        _secondary = Add(this, new HeroDetailsView { Name = "HeroDetails", ZIndex = 9 });
        _sellDrop = Add(this, new SellDropZone { Name = "SellDropZone", ZIndex = 20 });
        _sellDrop.PreviewRequested = PreviewSale;
        _sellDrop.DropRequested += DropSale;
        _heroes.Selected += SelectHero; _encounters.Selected += SelectEncounter; _events.Selected += SelectEvent;
        _shop.BuyRequested += Buy; _leave.Requested += RequestAction;
        _shop.DetailsRequested += ShowDetails; _details.SellRequested += Sell;
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
        _details.Hide();
        _sellDrop.Render(view.Player, view.BoardEnabled);
        _secondary.Render(view);
        _top.Render(view.Player, view.DisplayRound, view.DisplayTurn);
        _portrait.Render(view.Playback is null ? view.Title : view.Enemy?.Hero?.DisplayName ?? "敌方",
            view.EncounterLevel, view.Page is MatchPage.Preparation or MatchPage.BattlePlayback ? view.Enemy?.Hero : null,
            view.Playback?.State.Opponent);
        _portrait.SetContext(view.ContextIllustration, view.Message);
        var activeNavigation = view.Page == MatchPage.Shop ? 1 : view.Page == MatchPage.Event ? 2
            : view.Page is MatchPage.Preparation or MatchPage.BattlePlayback or MatchPage.BattleResult or MatchPage.MatchEnded ? 3 : 0;
        for (var index = 0; index < _navigation.GetChildCount(); index++)
            _navigation.GetChild<Label>(index).AddThemeColorOverride("font_color", index == activeNavigation ? MatchTheme.Gold : MatchTheme.Muted);
        _hero.Render(view.Player?.Hero); _progress.Render(view.Player); _leave.Render(view);
        _hero.RenderBattle(view.Playback?.State.Player);
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
        _enemy.Render(view.EnemyVisible && view.Page is MatchPage.Preparation or MatchPage.BattlePlayback ? view.Enemy : null,
            BoardZone.Battlefield, false, null, view.Playback is null ? "敌方战场"
                : $"战斗 {view.Playback.Tick / 10m:0.0}s{(view.Playback.Paused ? " · 暂停" : "")}{(view.Playback.State.Eclipse ? " · 日蚀" : "")}");
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
            _details.ShowSelected(selected, view.Player!.MatchId, view.Sell, GetLocalMousePosition(), Size);
        }
    }

    public override void _ExitTree()
    {
        Resized -= LayoutShell;
        _catalogButton.Pressed -= OpenCatalog; _catalog.Closed -= CloseCatalog;
        _skillCatalogButton.Pressed -= OpenSkillCatalog; _skillCatalog.Closed -= CloseSkillCatalog;
        _heroes.Selected -= SelectHero; _encounters.Selected -= SelectEncounter; _events.Selected -= SelectEvent;
        _shop.BuyRequested -= Buy; _leave.Requested -= RequestAction;
        _shop.DetailsRequested -= ShowDetails; _details.SellRequested -= Sell;
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
    private void ShowDetails(CardSnapshot card) => _details.ShowCard(card, GetLocalMousePosition(), Size);
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
        { _details.Hide(); _secondary.Hide(); CancelRequested?.Invoke(); GetViewport().SetInputAsHandled(); }
    }

    private static T Add<T>(Control parent, T child) where T : Control { parent.AddChild(child); return child; }

    private void LayoutShell()
    {
        const float margin = 18, gap = 14, top = 90;
        var heroHeight = Mathf.Clamp(Size.Y * .13f, 144, 170);
        var phaseHeight = Mathf.Clamp(Size.Y * .19f, 156, 240);
        var available = Mathf.Max(1, Size.Y - top - margin);
        // 阶段与双棋盘共用居中宽度；剩余高度限制格宽，底部英雄栏始终可见。
        var width = Mathf.Max(1, Mathf.Min(Mathf.Min(1400, Size.X - margin * 2),
            (available - heroHeight - phaseHeight - gap * 3 - 56) * _capacity / 4));
        var left = (Size.X - width) / 2;
        var side = Mathf.Clamp(width * .19f, 148, 230);
        var boardHeight = width / _capacity * 2 + 28;
        var heroTop = top + phaseHeight + boardHeight * 2 + gap * 3;
        _stageRect = new Rect2(left - 6, top - 6, width + 12, phaseHeight + 12);
        _heroRect = new Rect2(left - 6, heroTop - 6, width + 12, heroHeight + 12);
        var topWidth = Mathf.Max(width, Mathf.Min(900, Size.X - margin * 2));
        _top.Position = new Vector2((Size.X - topWidth) / 2, margin); _top.Size = new Vector2(topWidth, 42);
        _navigation.Position = new Vector2(left, 65); _navigation.Size = new Vector2(width, 22);
        // 保留场景分组与输入路径，分组本身不拦截右侧内容或侧栏输入。
        foreach (var path in new[] { "ContextRow", "BattlefieldRow", "BenchRow" })
        {
            var row = GetNode<Control>(path); row.Position = Vector2.Zero; row.Size = Size;
            row.MouseFilter = MouseFilterEnum.Ignore;
            foreach (Control cell in row.GetChildren()) cell.MouseFilter = MouseFilterEnum.Ignore;
        }
        Place("BenchRow/Hero", left + 12, heroTop + 6, width - 210, heroHeight - 12);
        Place("BenchRow/Progress", left + 120, heroTop + heroHeight - 20, width - 330, 20);
        _hero.Size = GetNode<Control>("BenchRow/Hero").Size;
        _progress.Size = GetNode<Control>("BenchRow/Progress").Size;
        _footerActions.Position = new Vector2(left + width - 188, heroTop + 12);
        _footerActions.Size = new Vector2(176, heroHeight - 24);
        Place("ContextRow/Portrait", left, top, side, phaseHeight);
        _portrait.Size = GetNode<Control>("ContextRow/Portrait").Size;
        Place("ContextRow/Leave", left + 10, top + phaseHeight - 60, side - 20, 54);
        if (_page is MatchPage.Preparation or MatchPage.BattlePlayback)
            Place("ContextRow/Leave", left - 164, top, 148, phaseHeight);
        _leaveScroll.Size = GetNode<Control>("ContextRow/Leave").Size;
        Place("ContextRow/ContextHost", left + side + 12, top, width - side - 12, phaseHeight);
        Place("BattlefieldRow/Content", left, top + phaseHeight + gap, width, boardHeight);
        Place("BenchRow/Content", left, top + phaseHeight + gap * 2 + boardHeight, width, boardHeight);
        foreach (var view in new Control[] { _heroes, _encounters, _events, _shop, _result, _enemy })
            view.Size = GetNode<Control>("ContextRow/ContextHost").Size;
        _battlefield.Size = _bench.Size = new Vector2(width, boardHeight);
        var choosing = _page == MatchPage.HeroSelection;
        GetNode<Control>("BattlefieldRow").Visible = GetNode<Control>("BenchRow").Visible = !choosing;
        GetNode<Control>("ContextRow/Portrait").Visible = GetNode<Control>("ContextRow/Leave").Visible = !choosing;
        _navigation.Visible = _footerActions.Visible = !choosing;
        if (choosing)
        {
            _top.Position = new Vector2(margin, margin); _top.Size = new Vector2(Size.X - margin * 2, 36);
            var host = GetNode<Control>("ContextRow/ContextHost");
            host.Position = new Vector2(margin, 70); host.Size = new Vector2(Size.X - margin * 2, Size.Y - 88);
            _heroes.Size = host.Size;
        }
        _catalogButton.Position = new Vector2(Size.X - margin - 328, margin);
        _catalogButton.Size = new Vector2(160, 40);
        _skillCatalogButton.Position = new Vector2(Size.X - margin - 160, margin);
        _skillCatalogButton.Size = new Vector2(160, 40);
        _catalog.Position = new Vector2(margin, margin);
        _catalog.Size = Size - Vector2.One * margin * 2;
        _skillCatalog.Position = _catalog.Position; _skillCatalog.Size = _catalog.Size;
        _details.ClampTo(Size);
        _sellDrop.Position = Vector2.Zero;
        _sellDrop.Size = new Vector2(Size.X, top + phaseHeight);
        _secondary.ClampTo(Size);
        QueueRedraw();
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
            MatchTheme.DrawSurface(this, _heroRect);
            MatchTheme.DrawSurface(this, _stageRect);
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
