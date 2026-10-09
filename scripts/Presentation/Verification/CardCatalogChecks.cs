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
            var open = shell.GetNode<Button>("OpenCardCatalog");
            var catalog = shell.GetNode<CardCatalogView>("CardCatalog");
            var search = catalog.GetNode<LineEdit>("Content/Filters/Search");
            var grid = catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards");
            var name = catalog.GetNode<Label>("Content/Body/DetailPanel/Details/CardName");
            var level = catalog.GetNode<OptionButton>("Content/Body/DetailPanel/Details/Level");
            var text = catalog.GetNode<RichTextLabel>("Content/Body/DetailPanel/Details/Effects");
            var choices = 0; shell.ChoiceSelected += (_, _, _) => choices++;
            if (!open.Visible || catalog.Visible || grid.GetChildCount() == 0) return Fail("初始入口或列表");
            open.EmitSignal(Button.SignalName.Pressed);
            if (!catalog.Visible || open.Visible || shell.GetNode<Control>("ContextRow/ContextHost/HeroSelectionView").Visible) return Fail("打开页面");
            var old = grid.GetChild<VBoxContainer>(0).GetNode<CardItemView>("Card");
            search.Text = "野猪";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            if (grid.GetChildCount() != 1 || !name.Text.Contains("野猪")) return Fail("搜索卡名");
            level.Select(level.ItemCount - 1); level.EmitSignal(OptionButton.SignalName.ItemSelected, level.ItemCount - 1);
            if (!text.GetParsedText().Contains("4级基础状态") || !text.GetParsedText().Contains("当前 100")) return Fail("等级与数值");
            old.EmitSignal(Button.SignalName.Pressed);
            if (!name.Text.Contains("野猪")) return Fail("旧按钮解绑");
            search.Text = "没有这张卡牌";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            if (grid.GetChildCount() != 0 || catalog.GetNode<Control>("Content/Body/DetailPanel/Details").Visible) return Fail("空结果");
            search.Text = "";
            search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
            var faction = catalog.GetNode<OptionButton>("Content/Classification/Faction");
            for (var index = 1; index < faction.ItemCount; index++)
            {
                faction.Select(index); faction.EmitSignal(OptionButton.SignalName.ItemSelected, index);
                if (grid.GetChildCount() == 0) return Fail("归属列表为空");
                foreach (VBoxContainer cell in grid.GetChildren())
                    if (!cell.GetChild<Label>(1).Text.EndsWith(" · " + faction.GetItemText(index), StringComparison.Ordinal)) return Fail("归属过滤");
            }
            if (!FiltersAndSorting(catalog)) return Fail("分类筛选与排序");
            catalog._Input(new InputEventKey { Pressed = true, Keycode = Key.Escape });
            if (catalog.Visible || !open.Visible || choices != 0) return Fail("Escape关闭与只读");
            open.EmitSignal(Button.SignalName.Pressed);
            catalog.GetNode<Button>("Content/Header/Back").EmitSignal(Button.SignalName.Pressed);
            shell.GetNode<Button>("ContextRow/ContextHost/HeroSelectionView/Choose").EmitSignal(Button.SignalName.Pressed);
            if (choices != 1 || open.Visible || catalog.Visible) return Fail("返回后选角");
            open.EmitSignal(Button.SignalName.Pressed);
            return !catalog.Visible;
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
        var faction = catalog.GetNode<OptionButton>("Content/Classification/Faction");
        var size = catalog.GetNode<OptionButton>("Content/Classification/Size");
        var element = catalog.GetNode<OptionButton>("Content/Classification/Element");
        var initial = catalog.GetNode<OptionButton>("Content/Classification/InitialLevel");
        var sort = catalog.GetNode<OptionButton>("Content/Classification/Sort");
        var direction = catalog.GetNode<Button>("Content/Classification/Direction");
        var canonical = new[] { GameElements.General, GameElements.Fire, GameElements.Water, GameElements.Wind,
            GameElements.Earth, GameElements.Lightning, GameElements.Wood, GameElements.Ice, GameElements.Light, GameElements.Dark };
        Pick(faction, 0);
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
        // 归属菜单按key顺序；按展示名定位夹具，避免依赖内容枚举顺序。
        for (var index = 1; index < faction.ItemCount; index++)
            if (faction.GetItemText(index) == dual.FactionName) Pick(faction, index);
        Pick(size, (int)CardSize.Large); Pick(element, 3);
        for (var index = 1; index < initial.ItemCount; index++)
            if (initial.GetItemId(index) == 4) Pick(initial, index);
        var search = catalog.GetNode<LineEdit>("Content/Filters/Search");
        search.Text = "双元素"; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
        if (!Matches([dual])) return false;
        catalog.Close(); catalog.Open(); catalog.SetEntries(entries);
        if (!Matches([dual])) return false;
        Pick(element, 8);
        if (grid.GetChildCount() != 0 || catalog.GetNode<Control>("Content/Body/DetailPanel/Details").Visible) return false;
        search.Text = ""; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
        Pick(faction, 0); Pick(size, 0); Pick(element, 0); Pick(initial, 0);
        var captions = entries.ToDictionary(Caption);
        for (var field = 0; field < sort.ItemCount; field++)
        {
            Pick(sort, field);
            foreach (var descending in new[] { false, true })
            {
                direction.ButtonPressed = descending;
                var shown = grid.GetChildren().Cast<VBoxContainer>().Select(cell => captions[cell.GetChild<Label>(1).Text]).ToArray();
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
        var previewLevel = catalog.GetNode<OptionButton>("Content/Body/DetailPanel/Details/Level");
        Pick(previewLevel, previewLevel.ItemCount - 1);
        var previewId = previewLevel.GetItemId(previewLevel.Selected);
        for (var index = 1; index < initial.ItemCount; index++)
            if (initial.GetItemId(index) == previewEntry.InitialLevel) Pick(initial, index);
        Pick(sort, 3); direction.ButtonPressed = true;
        if (!Matches([previewEntry]) || previewLevel.GetItemId(previewLevel.Selected) != previewId) return false;
        search.Text = ""; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
        Pick(initial, 0); direction.ButtonPressed = false; Pick(sort, 0);
        return Matches(formal);

        static string Caption(CardCatalogEntry entry) => $"{entry.InitialCard.DisplayName} · {entry.FactionName}";
        static void Pick(OptionButton option, int index) { option.Select(index); option.EmitSignal(OptionButton.SignalName.ItemSelected, index); }
        bool Matches(System.Collections.Generic.IEnumerable<CardCatalogEntry> expected) => grid.GetChildren().Cast<VBoxContainer>()
            .Select(cell => cell.GetChild<Label>(1).Text).OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(expected.Select(Caption).OrderBy(value => value, StringComparer.Ordinal));
        int Compare(CardCatalogEntry left, CardCatalogEntry right, int field)
        {
            if (field == 0) return StringComparer.Ordinal.Compare(left.InitialCard.FactionKey.ToString(), right.InitialCard.FactionKey.ToString());
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
            var search = catalog.GetNode<LineEdit>("Content/Filters/Search");
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                owner.GetWindow().Size = size; owner.GetTree().Root.ContentScaleSize = size;
                await Frame(); await Frame();
                await Save("entry");
                var open = shell.GetNode<Button>("OpenCardCatalog"); open.GrabFocus();
                KeyInput(Key.Enter); await Frame();
                if (!catalog.Visible || !search.HasFocus()) throw new InvalidOperationException("键盘打开图鉴或搜索焦点失败。");
                foreach (var character in "野猪") KeyInput(Key.None, character);
                await Frame(); await Frame();
                if (!catalog.GetNode<Label>("Content/Body/DetailPanel/Details/CardName").Text.Contains("野猪"))
                    throw new InvalidOperationException("真实键盘搜索未刷新详情。");
                foreach (var (query, label) in new[] { ("", "all"), ("羽饰头盔", "small"), ("登神者", "medium"), ("禅光寺", "large"), ("没有这张卡牌", "empty") })
                {
                    search.Text = query; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
                    await Frame(); await Frame();
                    var preview = catalog.GetNode<Control>("Content/Body/DetailPanel/Details/Preview");
                    var effects = catalog.GetNode<Control>("Content/Body/DetailPanel/Details/Effects");
                    if (catalog.Size.X > size.X || catalog.Size.Y > size.Y || effects.Size.Y < 100
                        || preview.GetGlobalRect().End.X > size.X || effects.GetGlobalRect().End.Y > size.Y)
                        throw new InvalidOperationException("图鉴窗口或详情溢出。");
                    await Save(label);
                }
                search.Text = ""; search.EmitSignal(LineEdit.SignalName.TextChanged, search.Text);
                var classification = catalog.GetNode<Control>("Content/Classification");
                var sort = catalog.GetNode<OptionButton>("Content/Classification/Sort");
                var direction = catalog.GetNode<Button>("Content/Classification/Direction");
                sort.GrabFocus(); KeyInput(Key.Enter); await Frame(); KeyInput(Key.Down); KeyInput(Key.Enter);
                await Frame(); await Frame();
                if (sort.Selected != 1) throw new InvalidOperationException("真实键盘排序选择未生效。");
                Click(direction); await Frame();
                if (!direction.ButtonPressed) throw new InvalidOperationException("真实鼠标降序切换未生效。");
                var sizeFilter = catalog.GetNode<OptionButton>("Content/Classification/Size");
                var elementFilter = catalog.GetNode<OptionButton>("Content/Classification/Element");
                sizeFilter.Select(2); sizeFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 2);
                elementFilter.Select(9); elementFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 9);
                await Frame(); await Frame();
                if (catalog.GetNode<GridContainer>("Content/Body/Browser/CardsScroll/Cards").GetChildCount() == 0
                    || classification.GetGlobalRect().End.X > size.X
                    || direction.GetGlobalRect().End.X > catalog.GetGlobalRect().End.X)
                    throw new InvalidOperationException("分类筛选结果为空或排序控件越界。");
                await Save("filtered");
                sizeFilter.Select(0); sizeFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
                elementFilter.Select(0); elementFilter.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
                sort.Select(0); sort.EmitSignal(OptionButton.SignalName.ItemSelected, 0); direction.ButtonPressed = false;
                KeyInput(Key.Escape); await Frame();
                if (catalog.Visible || !open.HasFocus()) throw new InvalidOperationException("Escape关闭或焦点恢复失败。");
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
