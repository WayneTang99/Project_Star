using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 图鉴正式等级与真实局外入口、只读交互及窗口布局验证（表现层）。
internal static class CardCatalogChecks
{
    internal static bool Definitions()
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var entries = CardCatalogQuery.Capture(registry);
        if (entries.Count != registry.Cards.Count || entries.Select(entry => entry.InitialCard.Key).Distinct().Count() != entries.Count) return false;
        foreach (var entry in entries)
        {
            var definition = registry.Cards[entry.InitialCard.Key];
            if (entry.SetName != (definition.Attributes.Identity.SetKey is { } setKey
                ? registry.Sets[setKey].Attributes.Identity.DisplayName : "")) return false;
            if (entry.InitialLevel != definition.InitialLevel || entry.FactionName.Length == 0
                || !entry.Levels.Select(level => level.Card.Level).SequenceEqual(Enumerable.Range(1, 5).Where(definition.SupportsLevel))) return false;
            foreach (var level in entry.Levels)
            {
                var card = level.Card;
                var expected = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, card.Level));
                if (card.Id != default || card.Illustration != definition.Attributes.Identity.Illustration
                    || card.Value != expected.Value || level.InitialValue != CardValueCalculator.CalculateInitialValue(definition, card.Level)
                    || !card.BaseValues.SequenceEqual(expected.BaseValues) || !card.CurrentValues.SequenceEqual(expected.CurrentValues)
                    || !card.ElementKeys.SequenceEqual(expected.ElementKeys) || card.Abilities.Count != expected.Abilities.Count
                    || !card.DescriptionEntries.SequenceEqual(expected.DescriptionEntries)) return false;
                _ = CardDisplayAdapter.Details(card); _ = CardDisplayAdapter.FaceEffects(card);
            }
        }
        return CardCatalogQuery.Capture(registry).Select(entry => entry.InitialCard.Key).SequenceEqual(entries.Select(entry => entry.InitialCard.Key));
    }

    internal static bool Interaction(Control owner)
    {
        var root = CreateRoot(owner);
        try
        {
            var shell = root.GetNode<MatchShell>("MatchShell");
            var open = shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenCardCatalog");
            var catalog = shell.GetNode<CardCatalogView>("CardCatalog");
            var search = catalog.GetNode<LineEdit>("Content/Body/Browser/Filters/Search");
            var grid = catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards");
            var preview = shell.GetNode<CardPreviewView>("CardPreview");
            var choices = 0; shell.ChoiceSelected += (_, _, _) => choices++;
            var menu = shell.GetNode<Button>("OpenGameMenu"); menu.EmitSignal(Button.SignalName.Pressed);
            if (!open.IsVisibleInTree() || catalog.Visible || grid.GetChildCount() == 0) return Fail("初始入口或列表");
            open.EmitSignal(Button.SignalName.Pressed);
            if (!catalog.Visible || open.IsVisibleInTree() || shell.GetNode<Control>("ContextRow/ContextHost/HeroSelectionView").Visible) return Fail("打开页面");
            var old = grid.GetChild<VBoxContainer>(0).GetNode<CardItemView>("Card");
            search.Text = "野猪";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            if (grid.GetChildCount() != 1 || catalog.HasNode("Content/Body/DetailPanel")) return Fail("搜索卡名");
            var card = grid.GetChild<VBoxContainer>(0).GetNode<CardItemView>("Card");
            card.EmitSignal(Control.SignalName.MouseEntered);
            if (!shell.GetNode<Control>("CardDetails").Visible) return Fail("悬停详情");
            card._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });
            if (!preview.Visible || !catalog.Visible || preview.DisplayedCard?.DisplayName != "野猪") return Fail("右键详情");
            preview.Close();
            old.EmitSignal(Button.SignalName.Pressed);
            search.Text = "没有这张卡牌";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            if (grid.GetChildCount() != 0) return Fail("空结果");
            search.Text = "";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            var faction = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Faction");
            for (var index = 1; index < faction.ItemCount; index++)
            {
                faction.Select(index); faction.EmitSignal(OptionButton.SignalName.ItemSelected, index);
                if (grid.GetChildCount() == 0) return Fail("归属列表为空");
                foreach (VBoxContainer cell in grid.GetChildren())
                    if (!CardCatalogQuery.Capture(DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly)).Any(entry => entry.FactionName == faction.GetItemText(index) && cell.GetNode<Label>("Caption").Text == $"{entry.InitialCard.DisplayName} · {entry.InitialLevel}级")) return Fail("归属过滤");
            }
            if (!FiltersAndSorting(catalog)) return Fail("分类筛选与排序");
            catalog._Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            if (catalog.Visible || !menu.HasFocus() || choices != 0) return Fail("Escape关闭与只读");
            open.EmitSignal(Button.SignalName.Pressed);
            catalog.GetNode<Button>("Content/Footer/Back").EmitSignal(Button.SignalName.Pressed);
            shell.GetNode<Button>("ContextRow/ContextHost/HeroSelectionView/Choose").EmitSignal(Button.SignalName.Pressed);
            if (choices != 1 || open.IsVisibleInTree() || catalog.Visible) return Fail("返回后选角");
            open.EmitSignal(Button.SignalName.Pressed);
            return catalog.Visible && choices == 1;
        }
        finally { owner.RemoveChild(root); root.Free(); }

        static bool Fail(string reason) { GD.Print($"图鉴交互失败：{reason}"); return false; }
    }

    private static bool FiltersAndSorting(CardCatalogView catalog)
    {
        var formal = CardCatalogQuery.Capture(DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly));
        var source = formal[0].InitialCard;
        var dual = new CardCatalogEntry("筛选夹具", 4, [new CardCatalogLevel(source with
        {
            Key = new StringName("ui.catalog_filter_dual"), DisplayName = "双元素筛选夹具", Level = 4,
            FactionKey = new StringName("ui.catalog_filter"),
            Size = CardSize.Large, ElementKeys = Array.AsReadOnly(new[] { GameElements.Fire, GameElements.Water }),
            Illustration = new StringName(""),
        }, 0)]);
        var entries = formal.Append(dual).ToArray(); catalog.SetEntries(entries);
        var grid = catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards");
        var faction = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Faction");
        var size = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Size");
        var element = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Element");
        var initial = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/InitialLevel");
        var set = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Set");
        var sort = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Sort");
        var direction = catalog.GetNode<Button>("Content/Body/Browser/Classification/Direction");
        var canonical = new[] { GameElements.General, GameElements.Fire, GameElements.Water, GameElements.Wind,
            GameElements.Earth, GameElements.Lightning, GameElements.Wood, GameElements.Ice, GameElements.Light, GameElements.Dark };
        Pick(faction, 0);
        var tagOptions = catalog.GetNode<HFlowContainer>("Content/Body/TagSidebar/Scroll/Filters/TagOptions");
        var tagMatch = catalog.GetNode<OptionButton>("Content/Body/TagSidebar/Scroll/Filters/TagMatch");
        var clearTags = catalog.GetNode<Button>("Content/Body/TagSidebar/Scroll/Filters/ClearTags");
        var availableTags = entries.SelectMany(entry => entry.InitialCard.Tags).Distinct().ToArray();
        if (tagOptions.GetChildCount() != availableTags.Length || !clearTags.Disabled || tagMatch.Selected != 0) return false;
        foreach (var tag in availableTags)
        {
            var button = tagOptions.GetNode<Button>(tag.ToString());
            button.ButtonPressed = true;
            if (!Matches(entries.Where(entry => entry.InitialCard.Tags.Contains(tag)))) return false;
            button.ButtonPressed = false;
        }
        var mechanical = tagOptions.GetNode<Button>(GameTags.Mechanical.ToString());
        var vehicle = tagOptions.GetNode<Button>(GameTags.Vehicle.ToString());
        mechanical.ButtonPressed = true; vehicle.ButtonPressed = true;
        foreach (var mode in new[] { 0, 1 })
        {
            Pick(tagMatch, mode);
            var expected = entries.Where(entry => mode == 0
                ? entry.InitialCard.Tags.Contains(GameTags.Mechanical) && entry.InitialCard.Tags.Contains(GameTags.Vehicle)
                : entry.InitialCard.Tags.Contains(GameTags.Mechanical) || entry.InitialCard.Tags.Contains(GameTags.Vehicle)).ToArray();
            if (expected.Length == 0 || !Matches(expected)) return false;
            var tagSearch = catalog.GetNode<LineEdit>("Content/Body/Browser/Filters/Search");
            tagSearch.Text = expected[0].InitialCard.DisplayName; tagSearch.EmitSignal(LineEdit.SignalName.TextChanged, tagSearch.Text);
            if (!Matches(expected.Where(entry => entry.InitialCard.DisplayName.Contains(tagSearch.Text)))) return false;
            tagSearch.Text = ""; tagSearch.EmitSignal(LineEdit.SignalName.TextChanged, "");
            Pick(size, (int)CardSize.Large);
            if (!Matches(expected.Where(entry => entry.InitialCard.Size == CardSize.Large))) return false;
            catalog.Close(); catalog.Open(); catalog.SetEntries(entries);
            if (tagMatch.Selected != mode || !mechanical.ButtonPressed || !vehicle.ButtonPressed
                || !Matches(expected.Where(entry => entry.InitialCard.Size == CardSize.Large))) return false;
            Pick(size, 0);
        }
        clearTags.EmitSignal(Button.SignalName.Pressed); Pick(tagMatch, 0);
        if (!clearTags.Disabled || mechanical.ButtonPressed || vehicle.ButtonPressed || !Matches(entries)) return false;
        var setKeys = entries.Where(entry => entry.InitialCard.SetKey is not null)
            .Select(entry => entry.InitialCard.SetKey!).Distinct()
            .OrderBy(key => key.ToString(), StringComparer.Ordinal).ToArray();
        if (set.ItemCount != setKeys.Length + 1 || set.GetItemText(0) != "全部套装") return false;
        for (var index = 1; index < set.ItemCount; index++)
        {
            Pick(set, index);
            var expected = entries.Where(entry => entry.InitialCard.SetKey == setKeys[index - 1]).ToArray();
            if (set.GetItemText(index) != expected[0].SetName) return false;
            if (!Matches(expected)) return false;
            var member = expected[0];
            Pick(initial, Enumerable.Range(1, initial.ItemCount - 1).First(item => initial.GetItemId(item) == member.InitialLevel));
            if (!Matches(expected.Where(entry => entry.InitialLevel == member.InitialLevel))) return false;
            catalog.Close(); catalog.Open(); catalog.SetEntries(entries);
            if (set.Selected != index || !Matches(expected.Where(entry => entry.InitialLevel == member.InitialLevel))) return false;
            Pick(initial, 0);
        }
        Pick(set, 0);
        for (var index = 1; index < size.ItemCount; index++)
        {
            Pick(size, index);
            if (!Matches(entries.Where(entry => (int)entry.InitialCard.Size == size.GetItemId(index)))) return false;
        }
        Pick(size, 0);
        for (var index = 1; index < element.ItemCount; index++)
        {
            Pick(element, index);
            if (!Matches(entries.Where(entry => entry.InitialCard.ElementKeys.Contains(canonical[index - 1])))) return false;
        }
        Pick(element, 0);
        for (var index = 1; index < initial.ItemCount; index++)
        {
            Pick(initial, index);
            if (!Matches(entries.Where(entry => entry.InitialLevel == initial.GetItemId(index)))) return false;
        }
        Pick(initial, 0);
        // 按展示名定位夹具，避免依赖内容枚举顺序。
        for (var index = 1; index < faction.ItemCount; index++)
            if (faction.GetItemText(index) == dual.FactionName) Pick(faction, index);
        Pick(size, (int)CardSize.Large); Pick(element, 3);
        for (var index = 1; index < initial.ItemCount; index++)
            if (initial.GetItemId(index) == 4) Pick(initial, index);
        var search = catalog.GetNode<LineEdit>("Content/Body/Browser/Filters/Search");
        search.Text = "双元素"; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
        if (!Matches([dual])) return false;
        catalog.Close(); catalog.Open(); catalog.SetEntries(entries);
        if (!Matches([dual])) return false;
        Pick(element, 8);
        if (grid.GetChildCount() != 0) return false;
        search.Text = ""; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
        Pick(faction, 0); Pick(size, 0); Pick(element, 0); Pick(initial, 0);
        var captions = entries.ToDictionary(Caption);
        for (var field = 0; field < sort.ItemCount; field++)
        {
            Pick(sort, field);
            foreach (var descending in new[] { false, true })
            {
                direction.ButtonPressed = descending;
                var shown = grid.GetChildren().Cast<VBoxContainer>().Select(cell => captions[cell.GetNode<Label>("Caption").Text]).ToArray();
                if (shown.Length != entries.Length) return false;
                for (var index = 1; index < shown.Length; index++)
                {
                    var comparison = Compare(shown[index - 1], shown[index], field);
                    if ((descending ? -comparison : comparison) > 0 || comparison == 0
                        && StringComparer.Ordinal.Compare(shown[index - 1].InitialCard.Key.ToString(), shown[index].InitialCard.Key.ToString()) > 0) return false;
                }
            }
        }
        direction.ButtonPressed = false; Pick(sort, 0); catalog.SetEntries(formal);
        var previewEntry = formal.First(entry => entry.Levels.Count > 1);
        search.Text = previewEntry.InitialCard.DisplayName; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
        for (var index = 1; index < initial.ItemCount; index++)
            if (initial.GetItemId(index) == previewEntry.InitialLevel) Pick(initial, index);
        Pick(sort, 3); direction.ButtonPressed = true;
        if (!Matches([previewEntry])) return false;
        search.Text = ""; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
        Pick(initial, 0); direction.ButtonPressed = false; Pick(sort, 0);
        return Matches(formal);

        static string Caption(CardCatalogEntry entry) => $"{entry.InitialCard.DisplayName} · {entry.InitialLevel}级";
        static void Pick(OptionButton option, int index) { option.Select(index); option.EmitSignal(OptionButton.SignalName.ItemSelected, index); }
        bool Matches(System.Collections.Generic.IEnumerable<CardCatalogEntry> expected) => grid.GetChildren().Cast<VBoxContainer>()
            .Select(cell => cell.GetNode<Label>("Caption").Text).OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(expected.Select(Caption).OrderBy(value => value, StringComparer.Ordinal));
        int Compare(CardCatalogEntry left, CardCatalogEntry right, int field)
        {
            if (field == 0)
            {
                var factions = new[] { "neutral", "paladin", "mona", "robin", "harla", "valos", "jiyun" };
                var leftKey = left.InitialCard.FactionKey.ToString();
                var rightKey = right.InitialCard.FactionKey.ToString();
                var leftRank = Array.IndexOf(factions, leftKey); var rightRank = Array.IndexOf(factions, rightKey);
                var comparison = (leftRank < 0 ? factions.Length : leftRank).CompareTo(rightRank < 0 ? factions.Length : rightRank);
                if (comparison != 0) return comparison;
                comparison = StringComparer.Ordinal.Compare(leftKey, rightKey);
                if (comparison != 0) return comparison;
                comparison = left.InitialLevel.CompareTo(right.InitialLevel);
                return comparison != 0 ? comparison : left.InitialCard.Size.CompareTo(right.InitialCard.Size);
            }
            if (field == 1) return left.InitialCard.Size.CompareTo(right.InitialCard.Size);
            if (field == 3) return left.InitialLevel.CompareTo(right.InitialLevel);
            for (var index = 0; index < Math.Min(left.InitialCard.ElementKeys.Count, right.InitialCard.ElementKeys.Count); index++)
            {
                var comparison = Array.IndexOf(canonical, left.InitialCard.ElementKeys[index]).CompareTo(Array.IndexOf(canonical, right.InitialCard.ElementKeys[index]));
                if (comparison != 0) return comparison;
            }
            return left.InitialCard.ElementKeys.Count.CompareTo(right.InitialCard.ElementKeys.Count);
        }
    }

    // 两种窗口、三种卡牌尺寸、长说明与空结果截图；几何检查不执行对局命令。
    internal static async Task Capture(Control owner)
    {
        var root = CreateRoot(owner);
        try
        {
            var shell = root.GetNode<MatchShell>("MatchShell");
            var catalog = shell.GetNode<CardCatalogView>("CardCatalog");
            var search = catalog.GetNode<LineEdit>("Content/Body/Browser/Filters/Search");
            if (OS.GetCmdlineUserArgs().Contains("--effect-scroll"))
            {
                catalog.Open(); await Frame(); await Frame();
                var scroll = catalog.GetNode<ScrollContainer>("Content/Body/Browser/CardsScroll");
                await Save("effects-before");
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                await Frame(); await Frame();
                var point = scroll.GetGlobalRect().GetCenter();
                for (var step = 0; step < 5; step++)
                {
                    owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.WheelDown, Pressed = true }, true);
                    owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.WheelDown, Pressed = false }, true);
                    await Frame();
                }
                if (scroll.ScrollVertical == 0) throw new InvalidOperationException("滚轮未推进图鉴。");
                await Save("effects-scrolled");
                search.Text = "萤火吉丁虫"; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
                await Frame(); await Frame();
                var texture = GD.Load<Texture2D>("res://art/ui/card-face/artwork/fireglow_jewel_beetle-illustration.png");
                using var pixels = texture.GetImage();
                if (!pixels.HasMipmaps()) throw new InvalidOperationException("萤火吉丁虫运行时纹理缺少mipmap。");
                GD.Print($"萤火吉丁虫 mipmap：{pixels.HasMipmaps()}，层数 {pixels.GetMipmapCount()}");
                foreach (var name in new[] { "dawnlight_gate", "venom_bride" })
                {
                    using var imported = GD.Load<Texture2D>($"res://art/ui/card-face/artwork/{name}-illustration.png").GetImage();
                    if (!imported.HasMipmaps()) throw new InvalidOperationException($"{name}重新导入后仍缺少mipmap。");
                    GD.Print($"{name} mipmap层数：{imported.GetMipmapCount()}");
                }
                await Save("beetle-mipmaps");
                var beetle = catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards").GetChild<VBoxContainer>(0).GetNode<CardItemView>("Card");
                beetle._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });
                await Frame(); await Frame(); await Save("beetle-preview-mipmaps");
                GD.Print("强制GC后真实滚轮与缩小原画截图完成。");
                return;
            }
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                owner.GetWindow().Size = size; owner.GetTree().Root.ContentScaleSize = size;
                await Frame(); await Frame();
                await Save("entry");
                shell.GetNode<Button>("OpenGameMenu").EmitSignal(Button.SignalName.Pressed);
                var open = shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenCardCatalog"); open.GrabFocus();
                KeyInput(Key.Enter); await Frame();
                if (!catalog.Visible || !search.HasFocus()) throw new InvalidOperationException("键盘打开图鉴或搜索焦点失败。");
                foreach (var character in "野猪") KeyInput(Key.None, character);
                await Frame(); await Frame();
                if (catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards").GetChildCount() != 1)
                    throw new InvalidOperationException("真实键盘搜索未刷新详情。");
                foreach (var (query, label) in new[] { ("", "all"), ("羽饰头盔", "small"), ("登神者", "medium"), ("禅光寺", "large"), ("没有这张卡牌", "empty") })
                {
                    search.Text = query; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
                    await Frame(); await Frame();
                    var cards = catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards");
                    if (catalog.Size.X > size.X || catalog.Size.Y > size.Y || catalog.HasNode("Content/Body/DetailPanel"))
                        throw new InvalidOperationException("图鉴窗口溢出或仍保留详情栏。");
                    if (query.Length > 0 && cards.GetChildCount() > 0)
                    {
                        var card = cards.GetChild<VBoxContainer>(0).GetNode<CardItemView>("Card");
                        card.EmitSignal(Control.SignalName.MouseEntered); await Frame(); await Frame();
                        await Save(label + "-hover");
                        card._GuiInput(new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Right });
                        await Frame(); await Frame(); await Save(label + "-detail");
                        shell.GetNode<CardPreviewView>("CardPreview").Close(); shell.HideHoverCard();
                    }
                    await Save(label);
                }
                search.Text = ""; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
                var classification = catalog.GetNode<Control>("Content/Body/TagSidebar");
                var setFilter = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Set");
                if (setFilter.ItemCount < 2) throw new InvalidOperationException("图鉴缺少正式套装筛选。");
                setFilter.Select(1); setFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
                await Frame(); await Frame();
                if (catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards").GetChildCount() == 0
                    || setFilter.GetGlobalRect().End.X > catalog.GetGlobalRect().End.X)
                    throw new InvalidOperationException("套装筛选结果为空或控件越界。");
                await Save("set-filter");
                setFilter.Select(0); setFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
                var mechanical = catalog.GetNode<Button>("Content/Body/TagSidebar/Scroll/Filters/TagOptions/Mechanical");
                var vehicle = catalog.GetNode<Button>("Content/Body/TagSidebar/Scroll/Filters/TagOptions/Vehicle");
                Click(mechanical); await Frame();
                vehicle.GrabFocus(); KeyInput(Key.Space); await Frame(); await Frame();
                var tagMatch = catalog.GetNode<OptionButton>("Content/Body/TagSidebar/Scroll/Filters/TagMatch");
                var formal = CardCatalogQuery.Capture(DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly));
                foreach (var mode in new[] { 0, 1 })
                {
                    tagMatch.Select(mode); tagMatch.EmitSignal(OptionButton.SignalName.ItemSelected, mode);
                    await Frame(); await Frame();
                    var expectedCount = formal.Count(entry => mode == 0
                        ? entry.InitialCard.Tags.Contains(GameTags.Mechanical) && entry.InitialCard.Tags.Contains(GameTags.Vehicle)
                        : entry.InitialCard.Tags.Contains(GameTags.Mechanical) || entry.InitialCard.Tags.Contains(GameTags.Vehicle));
                    if (!mechanical.ButtonPressed || !vehicle.ButtonPressed
                        || catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards").GetChildCount() != expectedCount
                        || catalog.GetNode<HFlowContainer>("Content/Body/TagSidebar/Scroll/Filters/TagOptions").GetChildren().OfType<Button>()
                            .Any(button => !catalog.GetGlobalRect().Encloses(button.GetGlobalRect())))
                        throw new InvalidOperationException("标签筛选鼠标／键盘选择、且或逻辑或按钮换行越界。");
                    await Save(mode == 0 ? "tags-and" : "tags-or");
                }
                Click(catalog.GetNode<Button>("Content/Body/TagSidebar/Scroll/Filters/ClearTags")); await Frame();
                tagMatch.Select(0); tagMatch.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
                var sort = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Sort");
                var direction = catalog.GetNode<Button>("Content/Body/Browser/Classification/Direction");
                sort.GrabFocus(); KeyInput(Key.Enter); await Frame(); KeyInput(Key.Down); KeyInput(Key.Enter);
                await Frame(); await Frame();
                if (sort.Selected != 1) throw new InvalidOperationException("真实键盘排序选择未生效。");
                Click(direction); await Frame();
                if (!direction.ButtonPressed) throw new InvalidOperationException("真实鼠标降序切换未生效。");
                var sizeFilter = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Size");
                var elementFilter = catalog.GetNode<OptionButton>("Content/Body/Browser/Classification/Element");
                sizeFilter.Select(2); sizeFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 2);
                elementFilter.Select(9); elementFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 9);
                await Frame(); await Frame();
                if (catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards").GetChildCount() == 0
                    || classification.GetGlobalRect().End.X > size.X
                    || direction.GetGlobalRect().End.X > catalog.GetGlobalRect().End.X)
                    throw new InvalidOperationException("分类筛选结果为空或排序控件越界。");
                await Save("filtered");
                var currentFocus = owner.GetViewport().GuiGetFocusOwner();
                var selectedSort = sort.Selected; var selectedCount = catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards").GetChildCount();
                foreach (var blue in new[] { true, false })
                {
                    shell.ApplyBoardSkin(MatchTheme.Skin); await Frame(); await Frame();
                    if (owner.GetViewport().GuiGetFocusOwner() != currentFocus || sort.Selected != selectedSort || !direction.ButtonPressed
                        || sizeFilter.Selected != 2 || elementFilter.Selected != 9
                        || catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards").GetChildCount() != selectedCount
                        || ((StyleBoxFlat)search.GetThemeStylebox("normal")).BgColor != new Color("262b34"))
                        throw new InvalidOperationException("卡牌图鉴切换配色后样式遗漏或筛选、排序、焦点改变。");
                    await Save(blue ? "filtered-blue" : "filtered-green-restored");
                }
                sizeFilter.Select(0); sizeFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
                elementFilter.Select(0); elementFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
                sort.Select(0); sort.EmitSignal(OptionButton.SignalName.ItemSelected, 0); direction.ButtonPressed = false;
                KeyInput(Key.Escape); await Frame();
                if (catalog.Visible || !shell.GetNode<Button>("OpenGameMenu").HasFocus()) throw new InvalidOperationException("Escape关闭或焦点恢复失败。");
            }
            GD.Print("图鉴两种窗口、三种卡牌尺寸、组合筛选、键盘排序、鼠标升降序、搜索与焦点检查通过。");
        }
        finally { owner.RemoveChild(root); root.Free(); }

        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        void Click(Button button)
        {
            var point = button.GetGlobalRect().GetCenter();
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        }
        void KeyInput(Key key, uint unicode = 0)
        {
            owner.GetViewport().PushInput(new InputEventKey { Keycode = key, Unicode = unicode, Pressed = true }, true);
            owner.GetViewport().PushInput(new InputEventKey { Keycode = key, Unicode = unicode, Pressed = false }, true);
        }
        async Task Save(string label)
        {
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            const string directory = "res://output/card-catalog";
            DirAccess.MakeDirRecursiveAbsolute(directory);
            using var image = owner.GetViewport().GetTexture().GetImage();
            if (owner.GetViewport().UseHdr2D)
                for (var y = 0; y < image.GetHeight(); y++)
                    for (var x = 0; x < image.GetWidth(); x++) image.SetPixel(x, y, image.GetPixel(x, y).LinearToSrgb());
            image.Convert(Image.Format.Rgba8);
            if (image.SavePng($"{directory}/{label}-{image.GetWidth()}x{image.GetHeight()}.png") != Error.Ok)
                throw new InvalidOperationException("图鉴截图保存失败。");
        }
    }

    private static MinimalPlaytest CreateRoot(Control owner)
    {
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        owner.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return root;
    }
}
