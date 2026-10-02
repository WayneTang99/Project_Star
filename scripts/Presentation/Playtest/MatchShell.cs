using System;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Presentation.Playtest;

// 三行共用列宽的界面骨架；子视图只接收快照并向入口分发操作意图。
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
    private KeyedActionView _heroes = null!;
    private KeyedActionView _encounters = null!;
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
        var host = GetNode<Control>("ContextRow/ContextHost");
        _heroes = Add(host, new KeyedActionView { Name = "HeroSelectionView" });
        _encounters = Add(host, new KeyedActionView { Name = "EncounterChoiceView" });
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
        _top.Render(view.Player); _portrait.Render(view.Playback is null ? view.Title
            : $"{view.Enemy?.Hero?.DisplayName ?? "敌方"}\n{PlaytestText.FormatHeroHud(view.Playback.State.Opponent)}");
        _hero.Render(view.Player?.Hero); _progress.Render(view.Player); _leave.Render(view);
        _hero.RenderBattle(view.Playback?.State.Player);
        _heroes.Render(view.Message, view.Heroes);
        _encounters.Render(view.Message, view.Choices);
        _events.Render(view.Message, view.EventOptions, view.EventRevision);
        _shop.Render(view.Message, view.Offers); _result.Render(view.Message, view.BattleLog);
        _heroes.Visible = view.Page == MatchPage.HeroSelection;
        _encounters.Visible = view.Page == MatchPage.EncounterChoice;
        _events.Visible = view.Page == MatchPage.Event;
        _shop.Visible = view.Page == MatchPage.Shop;
        _result.Visible = view.Page is MatchPage.BattleResult or MatchPage.MatchEnded;
        var capacity = Math.Max(view.Player?.BattlefieldCapacity ?? 0,
            Math.Max(view.Player?.BenchCapacity ?? 0, view.Enemy?.BattlefieldCapacity ?? 0));
        _battlefield.SharedCapacity = _bench.SharedCapacity = _enemy.SharedCapacity = capacity;
        _battlefield.Render(view.Player, BoardZone.Battlefield, view.BoardEnabled, view.SelectedCardId, "我方战场");
        _bench.Render(view.Player, BoardZone.Bench, view.BoardEnabled, view.SelectedCardId, "备战区");
        _enemy.Render(view.EnemyVisible && view.Page is MatchPage.Preparation or MatchPage.BattlePlayback ? view.Enemy : null,
            BoardZone.Battlefield, false, null, view.Playback is null ? "敌方战场"
                : $"战斗 {view.Playback.Tick / 10m:0.0}s{(view.Playback.Paused ? " · 暂停" : "")}{(view.Playback.State.Eclipse ? " · 日蚀" : "")}");
        _battlefield.RenderBattle(view.Playback); _enemy.RenderBattle(view.Playback); _bench.RenderBattle(view.Playback);
        if (_page != view.Page) { _battlefield.ClearFeedback(); _bench.ClearFeedback(); _enemy.ClearFeedback(); }
        _page = view.Page;
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
        _heroes.Selected -= SelectHero; _encounters.Selected -= SelectEncounter; _events.Selected -= SelectEvent;
        _shop.BuyRequested -= Buy; _leave.Requested -= RequestAction;
        _shop.DetailsRequested -= ShowDetails; _details.SellRequested -= Sell;
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
        var margin = 16f; var side = Mathf.Clamp(Size.X * .15f, 130, 220);
        var center = Mathf.Max(1, Size.X - side * 2 - margin * 4);
        var rowHeight = Mathf.Max(1, (Size.Y - 64 - margin * 4) / 3);
        _top.Position = new Vector2(margin, margin); _top.Size = new Vector2(Size.X - margin * 2, 40);
        var rows = new[] { "ContextRow", "BattlefieldRow", "BenchRow" };
        for (var index = 0; index < rows.Length; index++)
        {
            var row = GetNode<Control>(rows[index]); row.Position = new Vector2(margin, 64 + index * (rowHeight + margin));
            row.Size = new Vector2(Size.X - margin * 2, rowHeight);
            var children = row.GetChildren();
            for (var column = 0; column < children.Count; column++)
            {
                var cell = (Control)children[column]; cell.Position = new Vector2(column == 0 ? 0 : column == 1 ? side + margin : side + center + margin * 2, 0);
                cell.Size = new Vector2(column == 1 ? center : side, rowHeight);
            }
        }
        _portrait.Size = _hero.Size = _progress.Size = _leaveScroll.Size = new Vector2(side, rowHeight);
        foreach (var view in new Control[] { _heroes, _encounters, _events, _shop, _result, _enemy, _battlefield, _bench })
            view.Size = new Vector2(center, rowHeight);
        _details.ClampTo(Size);
        _sellDrop.Position = Vector2.Zero;
        _sellDrop.Size = new Vector2(Size.X, 64 + rowHeight);
        _secondary.ClampTo(Size);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Theme is null) return;
        var panel = Theme.GetStylebox("panel", "PanelContainer");
        DrawStyleBox(panel, new Rect2(_top.Position - Vector2.One * 6, _top.Size + Vector2.One * 12));
        foreach (var path in new[] { "ContextRow/Portrait", "ContextRow/ContextHost", "ContextRow/Leave", "BenchRow/Hero", "BenchRow/Progress" })
        {
            var cell = GetNode<Control>(path);
            DrawStyleBox(panel, new Rect2(cell.GlobalPosition - GlobalPosition - Vector2.One * 6, cell.Size + Vector2.One * 12));
        }
    }
}
