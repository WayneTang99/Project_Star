using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 分类与局外条目只消费冻结快照；怪物卡组每个怪物独立一行。
public sealed partial class AtlasHubView : PanelContainer
{
    public event Action? Closed, CardsRequested, SkillsRequested;
    public event Action<CardSnapshot, Rect2>? CardRequested;
    public event Action<SkillSnapshot>? SkillRequested;
    private readonly VBoxContainer _column = new();
    private readonly Label _title = new() { Text = "图鉴", SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Button _back = new() { Name = "Back", Text = "返回大厅" };
    private readonly LineEdit _search = new() { Name = "Search", PlaceholderText = "搜索", Visible = false };
    private readonly ScrollContainer _scroll = new() { SizeFlagsVertical = SizeFlags.ExpandFill,
        HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    private readonly HBoxContainer _body = new() { SizeFlagsVertical = SizeFlags.ExpandFill };
    private readonly ScrollContainer _detail = new() { Name = "EntryDetail", CustomMinimumSize = new Vector2(400, 0),
        SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Visible = false };
    private readonly VBoxContainer _detailColumn = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private Button? _entryFocus;
    private readonly VBoxContainer _rows = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly GridContainer _categories = new() { Name = "Categories", Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Dictionary<StringName, Button> _categoryButtons = new();
    private readonly CardDisplayAdapter _adapter = new();
    private IReadOnlyList<WorldCatalogEntry> _entries = Array.Empty<WorldCatalogEntry>();
    private StringName _category = new("");
    private static readonly StringName Cards = new("cards"), Skills = new("skills");
    private static readonly (StringName Key, string Name, string Icon)[] Categories =
    [ (Cards, "卡牌", "▱"), (Skills, "技能", "✧"), (WorldCatalogQuery.Heroes, "英雄", "♙"),
      (WorldCatalogQuery.Monsters, "怪物", "♜"), (WorldCatalogQuery.Encounters, "遭遇", "◇"),
      (WorldCatalogQuery.Mentors, "导师", "✦"), (WorldCatalogQuery.Sets, "套装", "▣") ];

    public override void _Ready()
    {
        var plate = MatchTheme.Surface(new Color("192a32"), Colors.Transparent); plate.SetCornerRadiusAll(0);
        plate.ContentMarginLeft = plate.ContentMarginRight = 36; plate.ContentMarginTop = plate.ContentMarginBottom = 28;
        AddThemeStyleboxOverride("panel", plate); AddChild(_column); _column.AddThemeConstantOverride("separation", 22);
        var header = new HBoxContainer(); _column.AddChild(header); header.AddChild(_title); header.AddChild(_back);
        MatchTheme.Text(_title, 28, MatchTheme.Gold); _back.Pressed += Back;
        _column.AddChild(_search); _search.TextChanged += _ => RenderRows();
        _column.AddChild(_body); _body.AddChild(_scroll); _body.AddChild(_detail); _detail.AddChild(_detailColumn);
        _scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill; _scroll.SizeFlagsStretchRatio = 2;
        _body.AddThemeConstantOverride("separation", 24); _scroll.AddChild(_rows); _rows.AddThemeConstantOverride("separation", 18);
        _column.AddChild(_categories); _column.MoveChild(_categories, 2);
        _categories.AddThemeConstantOverride("h_separation", 20); _categories.AddThemeConstantOverride("v_separation", 20);
        foreach (var category in Categories)
        {
            var button = new Button { Name = category.Key.ToString(), Text = category.Icon + "\n\n" + category.Name,
                CustomMinimumSize = new Vector2(0, 190), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            button.AddThemeFontSizeOverride("font_size", 24); _categories.AddChild(button);
            var key = category.Key; button.Pressed += () => OpenCategory(key); _categoryButtons[key] = button;
        }
        Resized += LayoutCategories; Hide(); LayoutCategories();
    }
    public void SetEntries(IReadOnlyList<WorldCatalogEntry> entries) => _entries = entries;
    public void Open() { Show(); ShowCategories(); }
    private void ShowCategories()
    {
        _category = new StringName(""); _title.Text = "图鉴"; _back.Text = "返回大厅";
        _categories.Show(); _search.Hide(); _body.Hide(); _detail.Hide(); _categoryButtons[Cards].GrabFocus();
    }
    public void ReturnFromCatalog(StringName category)
    { Show(); ShowCategories(); if (_categoryButtons.TryGetValue(category, out var button)) button.GrabFocus(); }
    public void OpenCategory(StringName category)
    {
        if (category == Cards) { Hide(); CardsRequested?.Invoke(); return; }
        if (category == Skills) { Hide(); SkillsRequested?.Invoke(); return; }
        _category = category; _title.Text = Categories.First(item => item.Key == category).Name + "图鉴";
        _back.Text = "返回分类"; _categories.Hide(); _search.Show(); _body.Show(); _scroll.Show(); _search.Text = "";
        RenderRows(); _search.GrabFocus();
    }
    private void Back()
    {
        if (_detail.Visible) { _detail.Hide(); _entryFocus?.GrabFocus(); return; }
        if (!_category.IsEmpty) { var selected = _category; ShowCategories(); _categoryButtons[selected].GrabFocus(); }
        else { Hide(); Closed?.Invoke(); }
    }
    private void RenderRows()
    {
        _detail.Hide();
        foreach (var child in _rows.GetChildren()) { _rows.RemoveChild(child); child.QueueFree(); }
        var query = _search.Text.Trim(); var matches = _entries.Where(entry => entry.Category == _category
            && (query.Length == 0 || (entry.Name + entry.Summary + string.Join(" ", entry.Cards.Select(card => card.DisplayName)))
                .Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (_category == WorldCatalogQuery.Monsters)
            foreach (var entry in matches) BuildEntry(_rows, entry, false);
        else
        {
            var grid = new GridContainer { Columns = Size.X < 1000 ? 3 : 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 16); grid.AddThemeConstantOverride("v_separation", 16); _rows.AddChild(grid);
            foreach (var entry in matches)
            {
                var button = new Button { CustomMinimumSize = new Vector2(0, 210), SizeFlagsHorizontal = SizeFlags.ExpandFill };
                grid.AddChild(button);
                var contents = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; button.AddChild(contents);
                contents.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); contents.OffsetLeft = contents.OffsetTop = 12;
                contents.OffsetRight = contents.OffsetBottom = -12;
                if (!entry.Illustration.IsEmpty && ResourceLoader.Exists(entry.Illustration.ToString()))
                    contents.AddChild(new TextureRect { Texture = _adapter.Artwork(entry.Illustration),
                        SizeFlagsVertical = SizeFlags.ExpandFill, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                        StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore });
                else contents.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
                var caption = new Label { Text = entry.Name, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
                MatchTheme.Text(caption, 18, MatchTheme.Gold); contents.AddChild(caption);
                button.Pressed += () => ShowEntry(entry, button);
            }
        }
        if (matches.Length == 0) _rows.AddChild(new Label { Text = "没有符合条件的条目" });
        _scroll.ScrollVertical = 0;
    }
    private void ShowEntry(WorldCatalogEntry entry, Button source)
    {
        foreach (var child in _detailColumn.GetChildren()) { _detailColumn.RemoveChild(child); child.QueueFree(); }
        _entryFocus = source;
        var back = new Button { Text = "返回列表" }; _detailColumn.AddChild(back); back.Pressed += Back;
        BuildEntry(_detailColumn, entry, true); _detail.Show(); _detail.ScrollVertical = 0; back.GrabFocus();
    }
    private void BuildEntry(Control host, WorldCatalogEntry entry, bool stacked)
    {
            var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", MatchTheme.Surface(new Color("22343c"), new Color("53676d")));
            host.AddChild(panel); BoxContainer row = stacked ? new VBoxContainer() : new HBoxContainer(); row.AddThemeConstantOverride("separation", 24); panel.AddChild(row);
            var identity = new VBoxContainer { CustomMinimumSize = new Vector2(230, 0) }; row.AddChild(identity);
            if (!entry.Illustration.IsEmpty && ResourceLoader.Exists(entry.Illustration.ToString()))
            {
                var art = new TextureRect { Texture = _adapter.Artwork(entry.Illustration), CustomMinimumSize = new Vector2(230, 145),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = MouseFilterEnum.Ignore }; identity.AddChild(art);
            }
            var name = new Label { Text = entry.Name }; MatchTheme.Text(name, 21, MatchTheme.Gold); identity.AddChild(name);
            var summary = new Label { Text = entry.Summary, CustomMinimumSize = new Vector2(230, 0),
                AutowrapMode = TextServer.AutowrapMode.WordSmart }; identity.AddChild(summary);
            var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; row.AddChild(body);
            if (entry.Category == WorldCatalogQuery.Sets && entry.Cards.Count > 0)
            {
                var rules = new RichTextLabel { FitContent = true, ScrollActive = false };
                body.AddChild(rules); CardKeywordText.Render(rules, CardDisplayAdapter.SetDetails(entry.Cards[0]));
            }
            if (entry.Cards.Count > 0)
            {
                var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 245),
                    VerticalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                body.AddChild(scroll); var cards = new HBoxContainer(); cards.AddThemeConstantOverride("separation", 10); scroll.AddChild(cards);
                foreach (var card in entry.Cards)
                {
                    var tile = new CardItemView { CustomMinimumSize = new Vector2(72 * (int)card.Size, 225) };
                    foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) tile.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
                    cards.AddChild(tile); tile.Render(card, _adapter); tile.DetailsRequested += (snapshot, rect) => CardRequested?.Invoke(snapshot, rect);
                    tile.Pressed += () => CardRequested?.Invoke(card, tile.GetGlobalRect());
                }
            }
            if (entry.Skills.Count > 0)
            {
                var skills = new HFlowContainer(); body.AddChild(skills);
                foreach (var skill in entry.Skills)
                {
                    var tile = new SkillItemView { CustomMinimumSize = new Vector2(64, 64) };
                    skills.AddChild(tile); tile.Render(skill, _adapter); tile.DetailsRequested += () => SkillRequested?.Invoke(skill);
                }
            }
    }
    private void LayoutCategories() => _categories.Columns = Size.X < 1000 ? 3 : 4;
    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree() || input is not InputEventKey { Pressed: true, Echo: false } key) return;
        // 上层详情自行处理 Escape；避免一次按键同时关闭两层。
        var shell = GetParent() as MatchShell;
        if (shell?.GetNode<CardPreviewView>("CardPreview").Visible == true || shell?.GetNode<HeroDetailsView>("HeroDetails").Visible == true) return;
        if (key.Keycode == Key.Escape) { Back(); GetViewport().SetInputAsHandled(); }
        else MatchMenuView.TrapTab(this, key);
    }
}
