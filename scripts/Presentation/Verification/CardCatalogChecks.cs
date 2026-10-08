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
            var faction = catalog.GetNode<OptionButton>("Content/Filters/Faction");
            for (var index = 1; index < faction.ItemCount; index++)
            {
                faction.Select(index); faction.EmitSignal(OptionButton.SignalName.ItemSelected, index);
                if (grid.GetChildCount() == 0) return Fail("归属列表为空");
                foreach (VBoxContainer cell in grid.GetChildren())
                    if (!cell.GetChild<Label>(1).Text.EndsWith(" · " + faction.GetItemText(index), StringComparison.Ordinal)) return Fail("归属过滤");
            }
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
                KeyInput(Key.Escape); await Frame();
                if (catalog.Visible || !open.HasFocus()) throw new InvalidOperationException("Escape关闭或焦点恢复失败。");
            }
            GD.Print("图鉴两种窗口、三种卡牌尺寸、键盘搜索与焦点检查通过。");
        }
        finally { owner.RemoveChild(root); root.Free(); }

        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
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
