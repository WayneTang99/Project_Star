using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Domain.Combat;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 局外卡牌图鉴：只读列表、筛选与等级预览，不接收对局模型（表现层）。
public sealed partial class CardCatalogView : PanelContainer
{
    public event Action? Closed;
    private readonly Button _back = new() { Name = "Back", Text = "返回英雄选择" };
    private readonly LineEdit _search = new() { Name = "Search", PlaceholderText = "搜索卡名、标签或效果", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly OptionButton _faction = new() { Name = "Faction", CustomMinimumSize = new Vector2(160, 0) };
    private readonly Label _count = new() { Name = "Count" };
    private readonly ScrollContainer _scroll = new() { Name = "CardsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    private readonly GridContainer _grid = new() { Name = "Cards", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _empty = new() { Name = "Empty", Text = "没有符合条件的卡牌", Visible = false };
    private readonly VBoxContainer _detail = new() { Name = "Details" };
    private readonly Label _name = new() { Name = "CardName", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly CardItemView _preview = new() { Name = "Preview", CustomMinimumSize = new Vector2(0, 220), MouseFilter = MouseFilterEnum.Ignore, FocusMode = FocusModeEnum.None };
    private readonly OptionButton _level = new() { Name = "Level" };
    private readonly RichTextLabel _text = new() { Name = "Effects", SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly CardDisplayAdapter _adapter = new();
    private readonly List<(CardItemView Button, Action Select, StringName Key)> _buttons = new();
    private IReadOnlyList<CardCatalogEntry> _entries = Array.Empty<CardCatalogEntry>();
    private IReadOnlyList<StringName> _factions = Array.Empty<StringName>();
    private CardCatalogEntry? _selected;
    private HBoxContainer _body = null!;
    private PanelContainer _detailPanel = null!;

    public override void _Ready()
    {
        var column = new VBoxContainer { Name = "Content" }; AddChild(column);
        var header = new HBoxContainer { Name = "Header" }; column.AddChild(header);
        var title = new Label { Text = "卡牌图鉴", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 24); header.AddChild(title); header.AddChild(_back);
        var filters = new HBoxContainer { Name = "Filters" }; column.AddChild(filters);
        filters.AddChild(_search); filters.AddChild(_faction); filters.AddChild(_count);
        _body = new HBoxContainer { Name = "Body", SizeFlagsVertical = SizeFlags.ExpandFill }; column.AddChild(_body);
        var browser = new VBoxContainer { Name = "Browser", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _body.AddChild(browser);
        browser.AddChild(_empty); browser.AddChild(_scroll); _scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        _scroll.AddChild(_grid);
        _detailPanel = new PanelContainer { Name = "DetailPanel" }; _body.AddChild(_detailPanel);
        _detailPanel.AddChild(_detail);
        foreach (var control in new Control[] { _name, _preview, _level, _text }) _detail.AddChild(control);
        _name.AddThemeFontSizeOverride("font_size", 20);
        _search.AddThemeColorOverride("font_color", MatchTheme.Ink);
        _search.AddThemeColorOverride("font_placeholder_color", new Color("6e818b"));
        _search.AddThemeColorOverride("caret_color", MatchTheme.Ink);
        _search.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("f8fbfc"), new Color("a5bcc9")));
        _search.AddThemeStyleboxOverride("focus", MatchTheme.Surface(new Color("f8fbfc"), MatchTheme.Blue));
        _back.Pressed += Close;
        _search.TextChanged += SearchChanged; _faction.ItemSelected += FactionChanged; _level.ItemSelected += LevelChanged;
        _body.Resized += LayoutColumns; _scroll.Resized += LayoutColumns;
        Hide();
    }

    // 入口注入应用查询的冻结条目，视图不访问内容目录。
    public void SetEntries(IReadOnlyList<CardCatalogEntry> entries)
    {
        _entries = entries;
        _factions = Array.AsReadOnly(entries.Select(entry => entry.InitialCard.FactionKey).Distinct().ToArray());
        _faction.Clear(); _faction.AddItem("全部归属");
        foreach (var faction in _factions) _faction.AddItem(entries.First(entry => entry.InitialCard.FactionKey == faction).FactionName);
        RefreshList();
    }

    // 打开时保留本次浏览筛选，将键盘焦点移入图鉴。
    public void Open() { Show(); LayoutColumns(); _search.GrabFocus(); }

    // 关闭图鉴只恢复局外视图，不发送对局操作。
    public void Close() { Hide(); Closed?.Invoke(); }

    private void SearchChanged(string _) => RefreshList();
    private void FactionChanged(long _) => RefreshList();
    private void LevelChanged(long index) => ShowLevel((int)index);

    private void RefreshList()
    {
        ClearButtons();
        var query = _search.Text.Trim();
        var factionIndex = _faction.Selected - 1;
        var filtered = _entries.Where(entry => (factionIndex < 0 || entry.InitialCard.FactionKey == _factions[factionIndex])
            && (query.Length == 0 || SearchText(entry).Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        _count.Text = $"{filtered.Length} / {_entries.Count} 张"; _empty.Visible = filtered.Length == 0;
        _scroll.ScrollVertical = 0;
        foreach (var entry in filtered)
        {
            var cell = new VBoxContainer { CustomMinimumSize = new Vector2(216, 0) }; _grid.AddChild(cell);
            var button = new CardItemView { Name = "Card", CustomMinimumSize = new Vector2(216, 190) }; cell.AddChild(button);
            button.Render(entry.InitialCard, _adapter);
            Action select = () => Select(entry); button.Pressed += select;
            _buttons.Add((button, select, entry.InitialCard.Key));
            cell.AddChild(new Label { Text = $"{entry.InitialCard.DisplayName} · {entry.FactionName}",
                HorizontalAlignment = HorizontalAlignment.Center, ClipText = true });
        }
        Select(filtered.FirstOrDefault(entry => entry.InitialCard.Key == _selected?.InitialCard.Key) ?? filtered.FirstOrDefault());
        LayoutColumns();
    }

    private static string SearchText(CardCatalogEntry entry) => entry.InitialCard.DisplayName + " " + entry.FactionName
        + " " + string.Join(" ", entry.InitialCard.Tags.Select(Project_Star.Domain.Definitions.TagDisplayNames.Get))
        + " " + string.Join(" ", entry.InitialCard.DescriptionEntries.Select(description =>
            Project_Star.Domain.Definitions.CardKeywords.DisplayName(description.KeywordKey) + " " + description.Text));

    private void Select(CardCatalogEntry? entry)
    {
        var previousLevel = _selected == entry && _level.Selected >= 0 ? _level.GetItemId(_level.Selected) : entry?.InitialLevel;
        _selected = entry; _detail.Visible = entry is not null;
        _level.Clear();
        foreach (var (button, _, _) in _buttons) button.SetSelected(false);
        if (entry is null) return;
        _name.Text = $"{entry.InitialCard.DisplayName} · {entry.FactionName}";
        foreach (var level in entry.Levels) _level.AddItem($"{level.Card.Level}级{(level.Card.Level == entry.InitialLevel ? " · 初始等级" : "")}", level.Card.Level);
        var index = Math.Max(0, entry.Levels.ToList().FindIndex(level => level.Card.Level == previousLevel));
        _level.Select(index); ShowLevel(index);
        var buttonIndex = _buttons.FindIndex(item => item.Key == entry.InitialCard.Key);
        if (buttonIndex >= 0) _buttons[buttonIndex].Button.SetSelected(true);
    }

    private void ShowLevel(int index)
    {
        if (_selected is null || index < 0 || index >= _selected.Levels.Count) return;
        var level = _selected.Levels[index]; var card = level.Card;
        _preview.Render(card, _adapter);
        var active = card.Abilities.Where(ability => ability.Activation == AbilityActivation.Active).ToArray();
        var timing = active.Length == 0 ? "无主动发动" : string.Join("\n", active.Select(ability =>
            $"冷却 {ability.CooldownTicks / 10m:0.##}秒 · 魔法消耗 {ability.ManaCost}"));
        CardKeywordText.Render(_text, $"{card.Level}级基础状态\n初始价值 {level.InitialValue} · 获得后价值 {card.Value}\n{timing}\n\n{CardDisplayAdapter.Details(card)}");
        _text.ScrollToLine(0);
    }

    private void LayoutColumns()
    {
        if (_detailPanel is null) return;
        _detailPanel.CustomMinimumSize = new Vector2(Mathf.Clamp(Size.X * .34f, 380, 540), 0);
        _grid.Columns = Math.Max(1, (int)((_scroll.Size.X - 16) / 224));
    }

    private void ClearButtons()
    {
        foreach (var (button, select, _) in _buttons) button.Pressed -= select;
        _buttons.Clear();
        foreach (var child in _grid.GetChildren()) { _grid.RemoveChild(child); child.QueueFree(); }
    }

    public override void _Input(InputEvent input)
    {
        if (IsVisibleInTree() && input is InputEventKey { Pressed: true, Keycode: Key.Escape })
        { Close(); GetViewport().SetInputAsHandled(); }
    }

    public override void _ExitTree()
    {
        _back.Pressed -= Close; _search.TextChanged -= SearchChanged;
        _faction.ItemSelected -= FactionChanged; _level.ItemSelected -= LevelChanged;
        _body.Resized -= LayoutColumns; _scroll.Resized -= LayoutColumns; ClearButtons();
    }
}
