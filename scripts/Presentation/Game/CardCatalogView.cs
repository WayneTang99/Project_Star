using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Game;

// 局外卡牌图鉴：只读列表、筛选与等级预览，不接收对局模型（表现层）。
public sealed partial class CardCatalogView : PanelContainer
{
    public event Action? Closed;
    private readonly Button _back = new() { Name = "Back", Text = "返回" };
    private readonly LineEdit _search = new() { Name = "Search", PlaceholderText = "搜索卡名、标签或效果", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly VBoxContainer _faction = new() { Name = "Faction", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private int _selectedFaction;
    private readonly Button _tagButton = new() { Name = "Tags", TooltipText = "标签筛选", CustomMinimumSize = new Vector2(38, 38) };
    private readonly Control _tagLayer = new() { Name = "TagLayer", Visible = false, ZIndex = 100 };
    private readonly PanelContainer _tagPanel = new() { Name = "TagPopup", CustomMinimumSize = new Vector2(320, 0) };
    private readonly OptionButton _size = new() { Name = "Size", CustomMinimumSize = new Vector2(110, 0) };
    private readonly OptionButton _element = new() { Name = "Element", CustomMinimumSize = new Vector2(110, 0) };
    private readonly OptionButton _initialLevel = new() { Name = "InitialLevel", CustomMinimumSize = new Vector2(150, 0) };
    private readonly OptionButton _set = new() { Name = "Set", CustomMinimumSize = new Vector2(130, 0) };
    private readonly OptionButton _sort = new() { Name = "Sort", CustomMinimumSize = new Vector2(130, 0) };
    private readonly Button _direction = new() { Name = "Direction", Text = "升序 ↑", ToggleMode = true };
    private readonly OptionButton _tagMatch = new() { Name = "TagMatch" };
    private readonly Button _clearTags = new() { Name = "ClearTags", Text = "清空标签" };
    private readonly HFlowContainer _tagOptions = new() { Name = "TagOptions" };
    private readonly HashSet<StringName> _selectedTags = new();
    private readonly Dictionary<StringName, Button> _tagButtons = new();
    private StringName[] _tags = [];
    private readonly Label _count = new() { Name = "Count" };
    private readonly ScrollContainer _scroll = new() { Name = "CardsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    private readonly GridContainer _grid = new() { Name = "Cards", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _empty = new() { Name = "Empty", Text = "没有符合条件的卡牌", Visible = false };
    private readonly CardDisplayAdapter _adapter = new();
    private readonly List<(CardItemView Button, Action Select, StringName Key)> _buttons = new();
    private IReadOnlyList<CardCatalogEntry> _entries = Array.Empty<CardCatalogEntry>();
    private IReadOnlyList<StringName> _factions = Array.Empty<StringName>();
    private IReadOnlyList<StringName> _sets = Array.Empty<StringName>();
    private static readonly (StringName Key, string Name)[] Elements =
    [
        (GameElements.General, "通用"), (GameElements.Fire, "火"), (GameElements.Water, "水"),
        (GameElements.Wind, "风"), (GameElements.Earth, "土"), (GameElements.Lightning, "雷"),
        (GameElements.Wood, "木"), (GameElements.Ice, "冰"), (GameElements.Light, "光"), (GameElements.Dark, "暗"),
    ];
    private HBoxContainer _body = null!;

    public override void _Ready()
    {
        var page = MatchTheme.Surface(new Color("40372f"), Colors.Transparent); page.SetBorderWidthAll(0); page.SetCornerRadiusAll(0);
        page.ContentMarginLeft = page.ContentMarginRight = page.ContentMarginTop = page.ContentMarginBottom = 28; AddThemeStyleboxOverride("panel", page);
        var column = new VBoxContainer { Name = "Content" }; column.AddThemeConstantOverride("separation", 18); AddChild(column);
        var header = new HBoxContainer { Name = "Header" }; column.AddChild(header);
        var title = new Label { Text = "卡牌图鉴", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 20); header.AddChild(title); header.AddChild(_back);
        var filters = new HBoxContainer { Name = "Filters" }; column.AddChild(filters);
        filters.AddChild(_search); filters.AddChild(_count);
        var classification = new HFlowContainer { Name = "Classification" }; column.AddChild(classification);
        foreach (var control in new Control[] { _size, _element, _initialLevel, _set }) classification.AddChild(control);

        classification.AddChild(_sort); classification.AddChild(_direction);
        var tagFilters = new HFlowContainer { Name = "TagFilters" }; column.AddChild(tagFilters);
        tagFilters.AddChild(new Label { Text = "标签" }); tagFilters.AddChild(_tagMatch); tagFilters.AddChild(_clearTags);

        _tagMatch.AddItem("且 · 包含全部"); _tagMatch.AddItem("或 · 包含任一");
        column.AddChild(_tagOptions);
        _size.AddItem("全部尺寸", 0);
        foreach (var size in Enum.GetValues<CardSize>()) _size.AddItem(TagDisplayNames.Get(GameTags.FromSize(size)), (int)size);
        _element.AddItem("全部元素");
        foreach (var (_, name) in Elements) _element.AddItem(name);
        foreach (var field in new[] { "归属", "尺寸", "元素", "初始等级" }) _sort.AddItem(field);
        _element.TooltipText = "双元素卡包含所选元素即可匹配";
        _initialLevel.TooltipText = "按正式定义的初始等级筛选，不受详情预览等级影响";
        _sort.TooltipText = "归属排序：无阵营、圣骑士、小魔女、冒险家、机械师、潜行者、熊猫人 → 初始等级1到5 → 尺寸小、中、大；元素排序按通用、火、水、风、土、雷、木、冰、光、暗，双元素再比较第二元素";
        _body = new HBoxContainer { Name = "Body", SizeFlagsVertical = SizeFlags.ExpandFill }; column.AddChild(_body);
        var browser = new VBoxContainer { Name = "Browser", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _body.AddChild(browser);
        browser.AddChild(_empty); browser.AddChild(_scroll); _scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        _scroll.AddChild(_grid);
        _search.AddThemeColorOverride("font_color", MatchTheme.Ink);
        _search.AddThemeColorOverride("font_placeholder_color", MatchTheme.Muted);
        _search.AddThemeColorOverride("caret_color", MatchTheme.Ink);
        _back.Pressed += Close;
        _search.TextChanged += SearchChanged;
        foreach (var filter in new[] { _size, _element, _initialLevel, _set, _sort }) filter.ItemSelected += FilterChanged;
        _direction.Toggled += DirectionChanged;
        _tagMatch.ItemSelected += FilterChanged; _clearTags.Pressed += ClearTags;
        _body.Resized += LayoutColumns; _scroll.Resized += LayoutColumns;
        var sidebar = new PanelContainer { Name = "TagSidebar", CustomMinimumSize = new Vector2(180, 0) };
        var rail = MatchTheme.Surface(Colors.Transparent, new Color("ad967633"));
        rail.SetBorderWidthAll(0); rail.BorderWidthRight = 1; rail.ContentMarginRight = 18;
        sidebar.AddThemeStyleboxOverride("panel", rail); _body.AddChild(sidebar); _body.MoveChild(sidebar, 0);
        var railScroll = new ScrollContainer { Name = "Scroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; sidebar.AddChild(railScroll);
        var railColumn = new VBoxContainer { Name = "Filters", SizeFlagsHorizontal = SizeFlags.ExpandFill }; railScroll.AddChild(railColumn);
        var factionSection = new VBoxContainer { Name = "HeroFaction", SizeFlagsHorizontal = SizeFlags.ExpandFill }; railColumn.AddChild(factionSection);
        var factionTitle = new Label { Text = "归属" }; MatchTheme.Text(factionTitle, 11, MatchTheme.Muted, spacing: 2); factionSection.AddChild(factionTitle);
        factionSection.AddChild(_faction); _faction.AddThemeConstantOverride("separation", 5);
        filters.Reparent(browser); browser.MoveChild(filters, 0);
        classification.Reparent(browser); browser.MoveChild(classification, 1);
        var footer = new HBoxContainer { Name = "Footer" }; column.AddChild(footer); _count.Reparent(footer); _count.SizeFlagsHorizontal = SizeFlags.ExpandFill; _back.Reparent(footer);
        _tagButton.Icon = HtmlReliefStyle.Icon("tags"); _tagButton.AddThemeConstantOverride("icon_max_width", 20); filters.AddChild(_tagButton);
        var clear = new Button { Name = "ClearFilters", Icon = HtmlReliefStyle.Icon("reset"), TooltipText = "清空筛选", CustomMinimumSize = new Vector2(38, 38) };
        clear.AddThemeConstantOverride("icon_max_width", 20); filters.AddChild(clear);
        clear.Pressed += () => { _search.Text = ""; SelectFaction(0); foreach (var filter in new[] { _size, _element, _initialLevel, _set, _sort, _tagMatch }) filter.Select(0); _direction.SetPressedNoSignal(false); _direction.Text = "升序 ↑"; ClearTags(); };
        AddChild(_tagLayer); _tagLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var dismiss = new Control { Name = "Dismiss" }; _tagLayer.AddChild(dismiss); dismiss.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        dismiss.GuiInput += input => { if (input is InputEventMouseButton { Pressed: true }) HideTags(); };
        _tagLayer.AddChild(_tagPanel); _tagPanel.AddThemeStyleboxOverride("panel", HtmlReliefStyle.Surface(margin: 18));
        HtmlReliefStyle.Shadow(_tagPanel);
        var tagColumn = new VBoxContainer { Name = "Filters", SizeFlagsHorizontal = SizeFlags.ExpandFill }; _tagPanel.AddChild(tagColumn);
        tagFilters.Reparent(tagColumn); _tagOptions.Reparent(tagColumn);
        tagFilters.GetChild(0).QueueFree(); _tagMatch.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _clearTags.Text = ""; _clearTags.Icon = HtmlReliefStyle.Icon("reset"); _clearTags.TooltipText = "清空标签";
        _clearTags.AddThemeConstantOverride("icon_max_width", 18);
        _tagButton.Pressed += () => { _tagLayer.Visible = !_tagLayer.Visible; if (_tagLayer.Visible) { LayoutTags(); _tagMatch.GrabFocus(); } };
        foreach (var button in new Button[] { _tagButton, clear, _back, _direction, _clearTags, _size, _element, _initialLevel, _set, _sort, _tagMatch }) HtmlReliefStyle.Button(button);
        _body.AddThemeConstantOverride("separation", 24); _grid.AddThemeConstantOverride("h_separation", 16); _grid.AddThemeConstantOverride("v_separation", 16);
        Hide();
    }

    // 入口注入应用查询的冻结条目，视图不访问内容目录。
    public void SetEntries(IReadOnlyList<CardCatalogEntry> entries)
    {
        _entries = entries;
        var previousFaction = _selectedFaction > 0 ? _factions[_selectedFaction - 1] : (StringName?)null;
        var previousSet = _set.Selected > 0 ? _sets[_set.Selected - 1] : (StringName?)null;
        var initialLevel = _initialLevel.Selected > 0 ? _initialLevel.GetItemId(_initialLevel.Selected) : 0;
        _factions = Array.AsReadOnly(entries.Select(entry => entry.InitialCard.FactionKey).Distinct()
            .OrderBy(FactionDisplayOrder.Rank)
            .ThenBy(key => key.ToString(), StringComparer.Ordinal).ToArray());
        foreach (var child in _faction.GetChildren()) { _faction.RemoveChild(child); child.QueueFree(); }
        _selectedFaction = Math.Max(0, _factions.ToList().FindIndex(key => key == previousFaction) + 1);
        for (var index = 0; index <= _factions.Count; index++)
        {
            var selectedIndex = index;
            var entry = index == 0 ? null : entries.First(item => item.InitialCard.FactionKey == _factions[index - 1]);
            var button = new Button { Name = index == 0 ? "All" : _factions[index - 1], ToggleMode = true,
                TooltipText = entry?.FactionName ?? "全部归属", CustomMinimumSize = new Vector2(0, 48) };
            HtmlReliefStyle.Button(button);
            var row = new HBoxContainer { Name = "Row", MouseFilter = MouseFilterEnum.Ignore };
            button.AddChild(row); row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); row.OffsetLeft = 10; row.OffsetRight = -10;
            row.AddThemeConstantOverride("separation", 10);
            var portrait = new TextureRect { CustomMinimumSize = new Vector2(30, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.LinearWithMipmaps,
                Texture = entry is not null && !entry.FactionIllustration.IsEmpty
                    ? PlayerHeroPanel.PortraitTexture(entry.FactionHeroKey, entry.FactionIllustration)
                    : HtmlReliefStyle.Icon(index == 0 ? "all" : "neutral") };
            row.AddChild(portrait);
            if (entry is not null && !entry.FactionIllustration.IsEmpty)
            {
                var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://scripts/Presentation/Game/CampPortrait.gdshader") };
                material.SetShaderParameter("portrait_size", 30f); portrait.Material = material;
            }
            var name = new Label { Text = entry?.FactionName ?? "全部归属", SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            MatchTheme.Text(name, 13, new Color("d1c2b0")); row.AddChild(name);
            var mark = new Label { Name = "Selected", Text = "✓", CustomMinimumSize = new Vector2(16, 0),
                SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
            MatchTheme.Text(mark, 12, new Color("dfc58b")); row.AddChild(mark);
            button.Pressed += () => SelectFaction(selectedIndex); _faction.AddChild(button);
        }
        UpdateFactionSelection();
        _sets = Array.AsReadOnly(entries.Where(entry => entry.InitialCard.SetKey is not null)
            .Select(entry => entry.InitialCard.SetKey!).Distinct()
            .OrderBy(key => key.ToString(), StringComparer.Ordinal).ToArray());
        _set.Clear(); _set.AddItem("全部套装");
        foreach (var set in _sets)
            _set.AddItem(entries.First(entry => entry.InitialCard.SetKey == set).SetName);
        _set.Select(Math.Max(0, _sets.ToList().FindIndex(key => key == previousSet) + 1));
        _initialLevel.Clear(); _initialLevel.AddItem("全部初始等级", 0);
        foreach (var level in entries.Select(entry => entry.InitialLevel).Distinct().OrderBy(level => level))
            _initialLevel.AddItem($"初始 {level} 级", level);
        for (var index = 0; index < _initialLevel.ItemCount; index++)
            if (_initialLevel.GetItemId(index) == initialLevel) _initialLevel.Select(index);
        var tags = entries.SelectMany(entry => entry.InitialCard.Tags).Distinct()
            .OrderBy(tag => tag == GameTags.Small ? 0 : tag == GameTags.Medium ? 1 : tag == GameTags.Large ? 2 : 3)
            .ThenBy(TagDisplayNames.Get, StringComparer.Ordinal).ToArray();
        _selectedTags.IntersectWith(tags);
        if (!_tags.SequenceEqual(tags))
        {
            _tags = tags;
            foreach (var child in _tagOptions.GetChildren()) { _tagOptions.RemoveChild(child); child.QueueFree(); }
            _tagButtons.Clear();
            foreach (var tag in tags)
            {
                var button = new Button { Name = tag.ToString(), Text = TagDisplayNames.Get(tag), ToggleMode = true,
                    ButtonPressed = _selectedTags.Contains(tag) };
                button.Toggled += selected =>
                {
                    if (selected) _selectedTags.Add(tag); else _selectedTags.Remove(tag);
                    RefreshList();
                };
                HtmlReliefStyle.Button(button);
                _tagButtons.Add(tag, button); _tagOptions.AddChild(button);
            }
        }
        RefreshList();
    }

    // 打开时保留本次浏览筛选，将键盘焦点移入图鉴。
    public void Open() { _tagLayer.Hide(); Show(); LayoutColumns(); _search.GrabFocus(); }

    // 关闭图鉴只恢复局外视图，不发送对局操作。
    public void Close() { _tagLayer.Hide(); (GetParent() as MatchShell)?.HideHoverCard(); Hide(); Closed?.Invoke(); }

    private void SelectFaction(int index)
    {
        _selectedFaction = index; UpdateFactionSelection(); RefreshList();
    }
    private void UpdateFactionSelection()
    {
        for (var index = 0; index < _faction.GetChildCount(); index++)
        {
            var button = _faction.GetChild<Button>(index); var selected = index == _selectedFaction;
            button.SetPressedNoSignal(selected);
            button.AddThemeStyleboxOverride("pressed", HtmlReliefStyle.Surface("selected"));
            button.GetNode<Label>("Row/Selected").Modulate = selected ? Colors.White : Colors.Transparent;
        }
    }
    private void LayoutTags()
    {
        _tagPanel.Size = new Vector2(320, 0);
        var anchor = GetGlobalTransform().AffineInverse() * _tagButton.GetGlobalRect().End;
        _tagPanel.Position = new Vector2(Mathf.Clamp(anchor.X - 320, 12, Mathf.Max(12, Size.X - 332)), anchor.Y + 10);
    }
    private void HideTags() { _tagLayer.Hide(); _tagButton.GrabFocus(); }
    private void SearchChanged(string _) => RefreshList();
    private void FilterChanged(long _) => RefreshList();
    private void DirectionChanged(bool descending) { _direction.Text = descending ? "降序 ↓" : "升序 ↑"; RefreshList(); }

    private void ClearTags()
    {
        _selectedTags.Clear();
        foreach (var button in _tagButtons.Values) button.SetPressedNoSignal(false);
        RefreshList();
    }

    private void RefreshList()
    {
        (GetParent() as MatchShell)?.HideHoverCard();
        ClearButtons();
        _clearTags.Disabled = _selectedTags.Count == 0;
        var query = _search.Text.Trim();
        var factionIndex = _selectedFaction - 1;
        var size = _size.GetItemId(_size.Selected);
        var elementIndex = _element.Selected - 1;
        var initialLevel = _initialLevel.GetItemId(_initialLevel.Selected);
        var setIndex = _set.Selected - 1;
        var filtered = _entries.Where(entry => (factionIndex < 0 || entry.InitialCard.FactionKey == _factions[factionIndex])
            && (size == 0 || (int)entry.InitialCard.Size == size)
            && (elementIndex < 0 || entry.InitialCard.ElementKeys.Contains(Elements[elementIndex].Key))
            && (initialLevel == 0 || entry.InitialLevel == initialLevel)
            && (setIndex < 0 || entry.InitialCard.SetKey == _sets[setIndex])
            && (_selectedTags.Count == 0 || (_tagMatch.Selected == 0
                ? _selectedTags.All(entry.InitialCard.Tags.Contains)
                : _selectedTags.Any(entry.InitialCard.Tags.Contains)))
            && (query.Length == 0 || SearchText(entry).Contains(query, StringComparison.OrdinalIgnoreCase)));
        var sorting = _direction.ButtonPressed ? filtered.OrderByDescending(SortValue) : filtered.OrderBy(SortValue);
        if (_sort.Selected == 0)
            sorting = _direction.ButtonPressed
                ? sorting.ThenByDescending(entry => entry.InitialLevel).ThenByDescending(entry => entry.InitialCard.Size)
                : sorting.ThenBy(entry => entry.InitialLevel).ThenBy(entry => entry.InitialCard.Size);
        var ordered = sorting.ThenBy(entry => entry.InitialCard.Key.ToString(), StringComparer.Ordinal).ToArray();
        _count.Text = $"{ordered.Length} / {_entries.Count} 张"; _empty.Visible = ordered.Length == 0;
        _scroll.ScrollVertical = 0;
        foreach (var entry in ordered)
        {
            var cell = new CatalogEntryView { CustomMinimumSize = new Vector2(205, 238), SizeFlagsHorizontal = SizeFlags.ExpandFill }; _grid.AddChild(cell);
            cell.AddChild(new Control { CustomMinimumSize = new Vector2(0, 18), MouseFilter = MouseFilterEnum.Ignore });
            var button = new CardItemView { Name = "Card", CustomMinimumSize = new Vector2(207, 138), SizeFlagsHorizontal = SizeFlags.ShrinkCenter }; cell.AddChild(button);
            foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            button.Render(entry.InitialCard, _adapter);
            button.MouseEntered += () => cell.ShowState("hover"); button.MouseExited += () => cell.ShowState("normal");
            button.FocusEntered += () => cell.ShowState("hover"); button.FocusExited += () => cell.ShowState("normal");
            button.ButtonDown += () => cell.ShowState("pressed"); button.ButtonUp += () => cell.ShowState("hover");
            Action select = () => { }; button.Pressed += select;
            _buttons.Add((button, select, entry.InitialCard.Key));
            cell.AddChild(new Label { Name = "Caption", Text = $"{entry.InitialCard.DisplayName} · {entry.InitialLevel}级",
                HorizontalAlignment = HorizontalAlignment.Center, ClipText = true });
            var tags = new Label { Text = string.Join(" · ", entry.InitialCard.Tags.Select(TagDisplayNames.Get)), HorizontalAlignment = HorizontalAlignment.Center, ClipText = true }; tags.AddThemeFontSizeOverride("font_size", 11); cell.AddChild(tags);
            foreach (var label in cell.GetChildren().OfType<Label>().Take(1)) label.AddThemeFontSizeOverride("font_size", 14);
        }
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

    private void LayoutColumns()
    {
        _grid.Columns = Math.Max(1, (int)((_scroll.Size.X - 16) / 219));
    }

    private void ClearButtons()
    {
        foreach (var (button, select, _) in _buttons) button.Pressed -= select;
        _buttons.Clear();
        foreach (var child in _grid.GetChildren()) { _grid.RemoveChild(child); child.QueueFree(); }
    }

    public override void _Input(InputEvent input)
    {
        if ((GetParent() as MatchShell)?.GetNode<Control>("CardPreview").Visible == true) return;
        if (IsVisibleInTree() && input is InputEventKey { Pressed: true, Keycode: Key.Tab } tab) MatchMenuView.TrapTab(_tagLayer.Visible ? _tagPanel : this, tab);
        if (IsVisibleInTree() && input is InputEventKey { Pressed: true, Keycode: Key.Escape })
        { if (_tagLayer.Visible) HideTags(); else Close(); GetViewport().SetInputAsHandled(); }
    }

    public override void _ExitTree()
    {
        _back.Pressed -= Close; _search.TextChanged -= SearchChanged;
        foreach (var filter in new[] { _size, _element, _initialLevel, _set, _sort }) filter.ItemSelected -= FilterChanged;
        _direction.Toggled -= DirectionChanged;
        _tagMatch.ItemSelected -= FilterChanged; _clearTags.Pressed -= ClearTags;
        _body.Resized -= LayoutColumns; _scroll.Resized -= LayoutColumns; ClearButtons();
    }
}
