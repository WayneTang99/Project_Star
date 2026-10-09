using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 局外技能图鉴展示方形原画与逐级效果，不接收可变对局（表现层）。
public sealed partial class SkillCatalogView : PanelContainer
{
    public event Action? Closed;
    private readonly Button _back = new() { Name = "Back", Text = "返回英雄选择" };
    private readonly LineEdit _search = new() { Name = "Search", PlaceholderText = "搜索技能名或效果", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly OptionButton _faction = new() { Name = "Faction", CustomMinimumSize = new Vector2(160, 0) };
    private readonly OptionButton _initialLevel = new() { Name = "InitialLevel", CustomMinimumSize = new Vector2(150, 0) };
    private readonly Label _count = new() { Name = "Count" };
    private readonly ScrollContainer _scroll = new() { Name = "SkillsScroll", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly GridContainer _grid = new() { Name = "Skills", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _empty = new() { Name = "Empty", Text = "没有符合条件的技能", Visible = false };
    private readonly VBoxContainer _detail = new() { Name = "Details" };
    private readonly Label _name = new() { Name = "SkillName", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly SkillItemView _preview = new() { Name = "Preview", CustomMinimumSize = new Vector2(0, 240), MouseFilter = MouseFilterEnum.Ignore, FocusMode = FocusModeEnum.None };
    private readonly OptionButton _level = new() { Name = "Level" };
    private readonly RichTextLabel _text = new() { Name = "Effects", SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly CardDisplayAdapter _adapter = new();
    private readonly List<(SkillItemView Button, Action Select, StringName Key)> _buttons = new();
    private IReadOnlyList<SkillCatalogEntry> _entries = Array.Empty<SkillCatalogEntry>();
    private IReadOnlyList<StringName> _factions = Array.Empty<StringName>();
    private SkillCatalogEntry? _selected;
    private PanelContainer _detailPanel = null!;

    public override void _Ready()
    {
        var column = new VBoxContainer { Name = "Content" }; AddChild(column);
        var header = new HBoxContainer { Name = "Header" }; column.AddChild(header);
        var title = new Label { Text = "技能图鉴", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 24); header.AddChild(title); header.AddChild(_back);
        var filters = new HBoxContainer { Name = "Filters" }; column.AddChild(filters);
        foreach (var control in new Control[] { _search, _faction, _initialLevel, _count }) filters.AddChild(control);
        _initialLevel.TooltipText = "按正式定义的初始等级筛选，不受详情预览等级影响";
        var body = new HBoxContainer { Name = "Body", SizeFlagsVertical = SizeFlags.ExpandFill }; column.AddChild(body);
        var browser = new VBoxContainer { Name = "Browser", SizeFlagsHorizontal = SizeFlags.ExpandFill }; body.AddChild(browser);
        browser.AddChild(_empty); browser.AddChild(_scroll); _scroll.AddChild(_grid);
        _detailPanel = new PanelContainer { Name = "DetailPanel" }; body.AddChild(_detailPanel); _detailPanel.AddChild(_detail);
        foreach (var control in new Control[] { _name, _preview, _level, _text }) _detail.AddChild(control);
        _name.AddThemeFontSizeOverride("font_size", 20);
        _search.AddThemeColorOverride("font_color", MatchTheme.Ink);
        _search.AddThemeColorOverride("font_placeholder_color", MatchTheme.Muted);
        _search.AddThemeColorOverride("caret_color", MatchTheme.Ink);
        _search.AddThemeStyleboxOverride("normal", MatchTheme.Surface(new Color("10211b"), new Color("756847")));
        _search.AddThemeStyleboxOverride("focus", MatchTheme.Surface(new Color("10211b"), MatchTheme.Blue));
        _back.Pressed += Close; _search.TextChanged += SearchChanged;
        _faction.ItemSelected += FilterChanged; _initialLevel.ItemSelected += FilterChanged; _level.ItemSelected += LevelChanged;
        Resized += LayoutColumns; _scroll.Resized += LayoutColumns; Hide();
    }

    // 入口注入应用查询结果，视图不访问内容定义。
    public void SetEntries(IReadOnlyList<SkillCatalogEntry> entries)
    {
        _entries = entries; _factions = Array.AsReadOnly(entries.Select(entry => entry.InitialSkill.FactionKey).Distinct().ToArray());
        _faction.Clear(); _faction.AddItem("全部归属");
        foreach (var faction in _factions) _faction.AddItem(entries.First(entry => entry.InitialSkill.FactionKey == faction).FactionName);
        _initialLevel.Clear(); _initialLevel.AddItem("全部初始等级", 0);
        foreach (var level in entries.Select(entry => entry.InitialLevel).Distinct().OrderBy(level => level)) _initialLevel.AddItem($"初始 {level} 级", level);
        RefreshList();
    }

    // 打开时保留浏览状态，将键盘焦点移入搜索。
    public void Open() { Show(); LayoutColumns(); _search.GrabFocus(); }
    // 返回只改变局外展示，不执行对局用例。
    public void Close() { Hide(); Closed?.Invoke(); }
    private void SearchChanged(string _) => RefreshList();
    private void FilterChanged(long _) => RefreshList();
    private void LevelChanged(long index) => ShowLevel((int)index);

    private void RefreshList()
    {
        ClearButtons(); var query = _search.Text.Trim(); var factionIndex = _faction.Selected - 1;
        var initialLevel = _initialLevel.GetItemId(_initialLevel.Selected);
        var filtered = _entries.Where(entry => (factionIndex < 0 || entry.InitialSkill.FactionKey == _factions[factionIndex])
            && (initialLevel == 0 || entry.InitialLevel == initialLevel)
            && (query.Length == 0 || (entry.FactionName + " " + string.Join(" ", entry.Levels.Select(CardDisplayAdapter.SkillDetails)))
                .Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        _count.Text = $"{filtered.Length} / {_entries.Count} 个"; _empty.Visible = filtered.Length == 0; _scroll.ScrollVertical = 0;
        foreach (var entry in filtered)
        {
            var cell = new VBoxContainer { CustomMinimumSize = new Vector2(172, 0) }; _grid.AddChild(cell);
            var button = new SkillItemView { Name = "Skill", CustomMinimumSize = new Vector2(172, 172) }; cell.AddChild(button);
            button.Render(entry.InitialSkill, _adapter);
            Action select = () => Select(entry); button.Pressed += select; _buttons.Add((button, select, entry.InitialSkill.Key));
            cell.AddChild(new Label { Text = entry.InitialSkill.DisplayName, HorizontalAlignment = HorizontalAlignment.Center });
            cell.AddChild(new Label { Text = $"{entry.FactionName} · 初始{entry.InitialLevel}级", HorizontalAlignment = HorizontalAlignment.Center });
        }
        Select(filtered.FirstOrDefault(entry => entry.InitialSkill.Key == _selected?.InitialSkill.Key) ?? filtered.FirstOrDefault());
        LayoutColumns();
    }

    private void Select(SkillCatalogEntry? entry)
    {
        var previousLevel = _selected == entry && _level.Selected >= 0 ? _level.GetItemId(_level.Selected) : entry?.InitialLevel;
        _selected = entry; _detail.Visible = entry is not null; _level.Clear();
        foreach (var (button, _, key) in _buttons) button.SetSelected(key == entry?.InitialSkill.Key);
        if (entry is null) return;
        _name.Text = $"{entry.InitialSkill.DisplayName} · {entry.FactionName}";
        foreach (var skill in entry.Levels) _level.AddItem($"{skill.Level}级{(skill.Level == entry.InitialLevel ? " · 初始等级" : "")}", skill.Level);
        var index = Math.Max(0, entry.Levels.ToList().FindIndex(skill => skill.Level == previousLevel));
        _level.Select(index); ShowLevel(index);
    }

    private void ShowLevel(int index)
    {
        if (_selected is null || index < 0 || index >= _selected.Levels.Count) return;
        var skill = _selected.Levels[index]; _preview.Render(skill, _adapter);
        CardKeywordText.Render(_text, CardDisplayAdapter.SkillDetails(skill)); _text.ScrollToLine(0);
    }

    private void LayoutColumns()
    {
        if (_detailPanel is null) return;
        _detailPanel.CustomMinimumSize = new Vector2(Mathf.Clamp(Size.X * .34f, 380, 540), 0);
        _grid.Columns = Math.Max(1, (int)((_scroll.Size.X - 16) / 180));
    }
    private void ClearButtons()
    {
        foreach (var (button, select, _) in _buttons) button.Pressed -= select; _buttons.Clear();
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
        _faction.ItemSelected -= FilterChanged; _initialLevel.ItemSelected -= FilterChanged; _level.ItemSelected -= LevelChanged;
        Resized -= LayoutColumns; _scroll.Resized -= LayoutColumns; ClearButtons();
    }
}
