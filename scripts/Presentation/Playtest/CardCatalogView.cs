using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 局外卡牌图鉴：只读列表、筛选与等级预览，不接收对局模型（表现层）。
public sealed partial class CardCatalogView : PanelContainer
{
    public event Action? Closed;
    private readonly Button _back = new() { Name = "Back", Text = "返回英雄选择" };
    private readonly LineEdit _search = new() { Name = "Search", PlaceholderText = "搜索卡名、标签或效果", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly OptionButton _faction = new() { Name = "Faction", CustomMinimumSize = new Vector2(160, 0) };
    private readonly OptionButton _size = new() { Name = "Size", CustomMinimumSize = new Vector2(110, 0) };
    private readonly OptionButton _element = new() { Name = "Element", CustomMinimumSize = new Vector2(110, 0) };
    private readonly OptionButton _initialLevel = new() { Name = "InitialLevel", CustomMinimumSize = new Vector2(150, 0) };
    private readonly OptionButton _sort = new() { Name = "Sort", CustomMinimumSize = new Vector2(130, 0) };
    private readonly Button _direction = new() { Name = "Direction", Text = "升序 ↑", ToggleMode = true };
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
    private static readonly (StringName Key, string Name)[] Elements =
    [
        (GameElements.General, "通用"), (GameElements.Fire, "火"), (GameElements.Water, "水"),
        (GameElements.Wind, "风"), (GameElements.Earth, "土"), (GameElements.Lightning, "雷"),
        (GameElements.Wood, "木"), (GameElements.Ice, "冰"), (GameElements.Light, "光"), (GameElements.Dark, "暗"),
    ];
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
        filters.AddChild(_search); filters.AddChild(_count);
        var classification = new HBoxContainer { Name = "Classification" }; column.AddChild(classification);
        foreach (var control in new Control[] { _faction, _size, _element, _initialLevel }) classification.AddChild(control);
        classification.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        classification.AddChild(new Label { Text = "排序" }); classification.AddChild(_sort); classification.AddChild(_direction);
        _size.AddItem("全部尺寸", 0);
        foreach (var size in Enum.GetValues<CardSize>()) _size.AddItem(TagDisplayNames.Get(GameTags.FromSize(size)), (int)size);
        _element.AddItem("全部元素");
        foreach (var (_, name) in Elements) _element.AddItem(name);
        foreach (var field in new[] { "归属", "尺寸", "元素", "初始等级" }) _sort.AddItem(field);
        _element.TooltipText = "双元素卡包含所选元素即可匹配";
        _initialLevel.TooltipText = "按正式定义的初始等级筛选，不受详情预览等级影响";
        _sort.TooltipText = "归属按筛选列表顺序；尺寸按小、中、大；元素按通用、火、水、风、土、雷、木、冰、光、暗，双元素再比较第二元素";
        _body = new HBoxContainer { Name = "Body", SizeFlagsVertical = SizeFlags.ExpandFill }; column.AddChild(_body);
        var browser = new VBoxContainer { Name = "Browser", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _body.AddChild(browser);
        browser.AddChild(_empty); browser.AddChild(_scroll); _scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        _scroll.AddChild(_grid);
        _detailPanel = new PanelContainer { Name = "DetailPanel" }; _body.AddChild(_detailPanel);
        _detailPanel.AddChild(_detail);
        foreach (var control in new Control[] { _name, _preview, _level, _text }) _detail.AddChild(control);
        _name.AddThemeFontSizeOverride("font_size", 20);
        _search.AddThemeColorOverride("font_color", MatchTheme.Ink);
        _search.AddThemeColorOverride("font_placeholder_color", MatchTheme.Muted);
        _search.AddThemeColorOverride("caret_color", MatchTheme.Ink);
        _search.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("10211b"), new Color("756847")));
        _search.AddThemeStyleboxOverride("focus", MatchTheme.Surface(new Color("10211b"), MatchTheme.Blue));
        _back.Pressed += Close;
        _search.TextChanged += SearchChanged; _level.ItemSelected += LevelChanged;
        foreach (var filter in new[] { _faction, _size, _element, _initialLevel, _sort }) filter.ItemSelected += FilterChanged;
        _direction.Toggled += DirectionChanged;
        _body.Resized += LayoutColumns; _scroll.Resized += LayoutColumns;
        Hide();
    }

    // 入口注入应用查询的冻结条目，视图不访问内容目录。
    public void SetEntries(IReadOnlyList<CardCatalogEntry> entries)
    {
        _entries = entries;
        var previousFaction = _faction.Selected > 0 ? _factions[_faction.Selected - 1] : (StringName?)null;
        var initialLevel = _initialLevel.Selected > 0 ? _initialLevel.GetItemId(_initialLevel.Selected) : 0;
        _factions = Array.AsReadOnly(entries.Select(entry => entry.InitialCard.FactionKey).Distinct()
            .OrderBy(key => key.ToString(), StringComparer.Ordinal).ToArray());
        _faction.Clear(); _faction.AddItem("全部归属");
        foreach (var faction in _factions) _faction.AddItem(entries.First(entry => entry.InitialCard.FactionKey == faction).FactionName);
        _faction.Select(Math.Max(0, _factions.ToList().FindIndex(key => key == previousFaction) + 1));
        _initialLevel.Clear(); _initialLevel.AddItem("全部初始等级", 0);
        foreach (var level in entries.Select(entry => entry.InitialLevel).Distinct().OrderBy(level => level))
            _initialLevel.AddItem($"初始 {level} 级", level);
        for (var index = 0; index < _initialLevel.ItemCount; index++)
            if (_initialLevel.GetItemId(index) == initialLevel) _initialLevel.Select(index);
        RefreshList();
    }

    // 打开时保留本次浏览筛选，将键盘焦点移入图鉴。
    public void Open() { Show(); LayoutColumns(); _search.GrabFocus(); }

    // 关闭图鉴只恢复局外视图，不发送对局操作。
    public void Close() { Hide(); Closed?.Invoke(); }

    private void SearchChanged(string _) => RefreshList();
    private void FilterChanged(long _) => RefreshList();
    private void DirectionChanged(bool descending) { _direction.Text = descending ? "降序 ↓" : "升序 ↑"; RefreshList(); }
    private void LevelChanged(long index) => ShowLevel((int)index);

    private void RefreshList()
    {
        ClearButtons();
        var query = _search.Text.Trim();
        var factionIndex = _faction.Selected - 1;
        var size = _size.GetItemId(_size.Selected);
        var elementIndex = _element.Selected - 1;
        var initialLevel = _initialLevel.GetItemId(_initialLevel.Selected);
        var filtered = _entries.Where(entry => (factionIndex < 0 || entry.InitialCard.FactionKey == _factions[factionIndex])
            && (size == 0 || (int)entry.InitialCard.Size == size)
            && (elementIndex < 0 || entry.InitialCard.ElementKeys.Contains(Elements[elementIndex].Key))
            && (initialLevel == 0 || entry.InitialLevel == initialLevel)
            && (query.Length == 0 || SearchText(entry).Contains(query, StringComparison.OrdinalIgnoreCase)));
        var ordered = (_direction.ButtonPressed ? filtered.OrderByDescending(SortValue) : filtered.OrderBy(SortValue))
            .ThenBy(entry => entry.InitialCard.Key.ToString(), StringComparer.Ordinal).ToArray();
        _count.Text = $"{ordered.Length} / {_entries.Count} 张"; _empty.Visible = ordered.Length == 0;
        _scroll.ScrollVertical = 0;
        foreach (var entry in ordered)
        {
            var cell = new VBoxContainer { CustomMinimumSize = new Vector2(216, 0) }; _grid.AddChild(cell);
            var button = new CardItemView { Name = "Card", CustomMinimumSize = new Vector2(216, 190) }; cell.AddChild(button);
            button.Render(entry.InitialCard, _adapter);
            Action select = () => Select(entry); button.Pressed += select;
            _buttons.Add((button, select, entry.InitialCard.Key));
            cell.AddChild(new Label { Text = $"{entry.InitialCard.DisplayName} · {entry.FactionName}",
                HorizontalAlignment = HorizontalAlignment.Center, ClipText = true });
        }
        Select(ordered.FirstOrDefault(entry => entry.InitialCard.Key == _selected?.InitialCard.Key) ?? ordered.FirstOrDefault());
        LayoutColumns();
    }

    private int SortValue(CardCatalogEntry entry) => _sort.Selected switch
    {
        1 => (int)entry.InitialCard.Size,
        2 => ElementOrder(entry.InitialCard.ElementKeys[0]) * (Elements.Length + 1)
            + (entry.InitialCard.ElementKeys.Count == 1 ? 0 : ElementOrder(entry.InitialCard.ElementKeys[1]) + 1),
        3 => entry.InitialLevel,
        _ => _factions.ToList().IndexOf(entry.InitialCard.FactionKey),
    };

    private static int ElementOrder(StringName key) => Array.FindIndex(Elements, element => element.Key == key);

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
        foreach (var filter in new[] { _faction, _size, _element, _initialLevel, _sort }) filter.ItemSelected -= FilterChanged;
        _direction.Toggled -= DirectionChanged; _level.ItemSelected -= LevelChanged;
        _body.Resized -= LayoutColumns; _scroll.Resized -= LayoutColumns; ClearButtons();
    }
}
