using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 技能原画身份贯穿定义、实例、快照与图鉴，以及真实局外交互验证（表现层）。
internal static class SkillCatalogChecks
{
    internal static bool ArtworkAndLevels(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var entries = SkillCatalogQuery.Capture(registry); var paths = new System.Collections.Generic.HashSet<StringName>();
        if (entries.Count != registry.Skills.Count || entries.Select(entry => entry.InitialSkill.Key).Distinct().Count() != entries.Count) return false;
        var icon = new SkillItemView(); owner.AddChild(icon); var adapter = new CardDisplayAdapter();
        try
        {
            foreach (var entry in entries)
            {
                var definition = registry.Skills[entry.InitialSkill.Key]; var identity = definition.Attributes.Identity;
                if (identity.Illustration.IsEmpty || !paths.Add(identity.Illustration)
                    || !ResourceLoader.Exists(identity.Illustration.ToString())
                    || !entry.Levels.Select(skill => skill.Level).SequenceEqual(Enumerable.Range(1, 5).Where(definition.SupportsLevel))) return false;
                var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
                if (texture.GetWidth() != texture.GetHeight() || texture.GetWidth() < 256) return false;
                foreach (var skill in entry.Levels)
                {
                    var session = new MatchSession(42); var random = session.Random.State;
                    var acquired = new SkillAcquisitionService(new EntityFactory()).AcquireSkill(session, definition, skill.Level);
                    if (acquired.IsFailure) return false;
                    var snapshot = MatchSnapshot.From(session).Skills.Single();
                    if (skill.Id != default || acquired.Value!.Skill.Attributes.Identity.Illustration != identity.Illustration
                        || snapshot.Illustration != skill.Illustration || snapshot.Illustration != identity.Illustration
                        || !skill.CurrentValues.SequenceEqual(snapshot.CurrentValues)
                        || CardDisplayAdapter.SkillDetails(skill) != CardDisplayAdapter.SkillDetails(snapshot)) return false;
                    icon.Render(skill, adapter);
                    if (icon.GetNode<TextureRect>("Artwork").Texture?.ResourcePath != identity.Illustration.ToString()) return false;
                    _ = SkillCatalogQuery.Capture(registry);
                    if (session.Random.State != random || session.Player.Skills.Items.Count != 1) return false;
                }
                GD.Print($"技能原画 {identity.DisplayName}：{texture.GetWidth()}×{texture.GetHeight()}");
            }
            icon.Render(entries[0].InitialSkill with { Illustration = new StringName("") }, adapter);
            if (icon.GetNode<TextureRect>("Artwork").Texture is null) return false;
            var details = new HeroDetailsView(); owner.AddChild(details);
            try
            {
                var view = SkillView(registry); details.Render(view); details.ShowSection(HeroSection.Skills, new Vector2(1280, 720));
                var rows = details.GetNode<VBoxContainer>("Content/Scroll/Items").GetChildren().OfType<HBoxContainer>().ToArray();
                return rows.Length == view.Player!.Skills.Count && rows.Select((row, index) =>
                    row.GetChildren().OfType<SkillItemView>().Single().GetNode<TextureRect>("Artwork").Texture?.ResourcePath
                        == view.Player.Skills[index].Illustration.ToString()).All(match => match);
            }
            finally { owner.RemoveChild(details); details.Free(); }
        }
        finally { owner.RemoveChild(icon); icon.Free(); }
    }

    internal static bool Interaction(Control owner)
    {
        var root = CreateRoot(owner);
        try
        {
            var shell = root.GetNode<MatchShell>("MatchShell"); var open = shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenSkillCatalog");
            var catalog = shell.GetNode<SkillCatalogView>("SkillCatalog");
            var search = catalog.GetNode<LineEdit>("Content/Filters/Search");
            var grid = catalog.GetNode<GridContainer>("Content/Body/Browser/SkillsScroll/Skills");
            var name = catalog.GetNode<Label>("Content/Body/DetailPanel/Details/SkillName");
            var level = catalog.GetNode<OptionButton>("Content/Body/DetailPanel/Details/Level");
            var text = catalog.GetNode<RichTextLabel>("Content/Body/DetailPanel/Details/Effects");
            var choices = 0; shell.ChoiceSelected += (_, _, _) => choices++;
            var menu = shell.GetNode<Button>("OpenGameMenu"); menu.EmitSignal(Button.SignalName.Pressed);
            if (!open.IsVisibleInTree() || catalog.Visible || grid.GetChildCount() == 0) return Fail("初始列表");
            open.EmitSignal(Button.SignalName.Pressed);
            if (!catalog.Visible || open.IsVisibleInTree() || shell.GetNode<Control>("CardCatalog").Visible
                || shell.GetNode<Control>("ContextRow/ContextHost/HeroSelectionView").Visible) return Fail("页面互斥");
            var old = grid.GetChild<VBoxContainer>(0).GetNode<SkillItemView>("Skill");
            Search("突袭");
            if (grid.GetChildCount() != 1 || !name.Text.Contains("突袭") || level.ItemCount != 4) return Fail("搜索");
            level.Select(3); level.EmitSignal(OptionButton.SignalName.ItemSelected, 3);
            if (!text.GetParsedText().Contains("20%") || !text.GetParsedText().Contains("4级")) return Fail("等级效果");
            old.EmitSignal(Button.SignalName.Pressed);
            if (!name.Text.Contains("突袭")) return Fail("旧按钮解绑");
            Search("放逐");
            if (level.ItemCount != 3 || level.GetItemId(0) != 2 || !text.GetParsedText().Contains("禁锢 1秒")) return Fail("受限等级");
            Search("至圣斩");
            if (level.ItemCount != 1 || level.GetItemId(0) != 4) return Fail("固定等级");
            Search("护甲");
            if (grid.GetChildCount() != 1 || !name.Text.Contains("捍卫")) return Fail("效果搜索");
            Search(""); var faction = catalog.GetNode<OptionButton>("Content/Filters/Faction");
            for (var index = 1; index < faction.ItemCount; index++)
            {
                Pick(faction, index);
                foreach (VBoxContainer cell in grid.GetChildren())
                    if (!cell.GetChild<Label>(2).Text.StartsWith(faction.GetItemText(index) + " · ")) return Fail("归属筛选");
            }
            Pick(faction, 0); var initial = catalog.GetNode<OptionButton>("Content/Filters/InitialLevel");
            Pick(initial, initial.ItemCount - 1);
            if (grid.GetChildCount() != 1 || !name.Text.Contains("至圣斩")) return Fail("初始等级筛选");
            Pick(initial, 0); Search("没有这个技能");
            if (grid.GetChildCount() != 0 || catalog.GetNode<Control>("Content/Body/DetailPanel/Details").Visible) return Fail("空结果");
            catalog.Close();
            if (catalog.Visible || !menu.HasFocus() || choices != 0) return Fail("关闭与只读");
            shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenCardCatalog").EmitSignal(Button.SignalName.Pressed);
            if (!shell.GetNode<Control>("CardCatalog").Visible || catalog.Visible) return Fail("卡牌图鉴保留");
            shell.GetNode<CardCatalogView>("CardCatalog").Close();
            shell.GetNode<Button>("ContextRow/ContextHost/HeroSelectionView/Choose").EmitSignal(Button.SignalName.Pressed);
            open.EmitSignal(Button.SignalName.Pressed);
            return choices == 1 && catalog.Visible && !open.IsVisibleInTree();

            void Search(string value) { search.Text = value; search.EmitSignal(LineEdit.SignalName.TextChanged, value); }
            static void Pick(OptionButton button, int index) { button.Select(index); button.EmitSignal(OptionButton.SignalName.ItemSelected, index); }
        }
        finally { owner.RemoveChild(root); root.Free(); }
        static bool Fail(string reason) { GD.Print($"技能图鉴交互失败：{reason}"); return false; }
    }

    // 窗口、真实键盘搜索、完整效果与图标比例检查，并保存代表截图。
    internal static async Task Capture(Control owner)
    {
        var root = CreateRoot(owner);
        try
        {
            var shell = root.GetNode<MatchShell>("MatchShell"); var open = shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenSkillCatalog");
            var catalog = shell.GetNode<SkillCatalogView>("SkillCatalog"); var search = catalog.GetNode<LineEdit>("Content/Filters/Search");
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                owner.GetWindow().Size = size; owner.GetTree().Root.ContentScaleSize = size;
                await Frame(); await Frame(); await Save("entry");
                shell.GetNode<Button>("OpenGameMenu").EmitSignal(Button.SignalName.Pressed);
                open.GrabFocus(); KeyInput(Key.Enter); await Frame();
                if (!catalog.Visible || !search.HasFocus()) throw new InvalidOperationException("技能图鉴键盘入口失败。");
                foreach (var character in "放逐") KeyInput(Key.None, character);
                await Frame(); await Frame();
                if (!catalog.GetNode<Label>("Content/Body/DetailPanel/Details/SkillName").Text.Contains("放逐"))
                    throw new InvalidOperationException("技能图鉴中文键盘搜索失败。");
                foreach (var (query, label) in new[] { ("", "all"), ("突袭", "assault"), ("放逐", "banish"), ("至圣斩", "divine-smite"), ("没有这个技能", "empty") })
                {
                    search.Text = query; search.EmitSignal(LineEdit.SignalName.TextChanged, query); await Frame(); await Frame();
                    var effects = catalog.GetNode<Control>("Content/Body/DetailPanel/Details/Effects");
                    if (effects.Size.Y < 100 || effects.GetGlobalRect().End.Y > size.Y || effects.GetGlobalRect().End.X > size.X)
                        throw new InvalidOperationException("技能详情溢出窗口。");
                    await Save(label);
                }
                search.Text = ""; search.EmitSignal(LineEdit.SignalName.TextChanged, ""); KeyInput(Key.Escape); await Frame();
                if (catalog.Visible || !shell.GetNode<Button>("OpenGameMenu").HasFocus()) throw new InvalidOperationException("技能图鉴返回焦点失败。");
            }
            shell.Render(SkillView(DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly)));
            shell.GetNode<HeroDetailsView>("HeroDetails").ShowSection(HeroSection.Skills, shell.Size);
            await Frame(); await Frame(); await Save("acquired-skills");
            GD.Print("技能图鉴两种窗口、真实键盘、详情与焦点检查通过。");
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
            const string directory = "res://output/skill-catalog"; DirAccess.MakeDirRecursiveAbsolute(directory);
            using var image = owner.GetViewport().GetTexture().GetImage();
            if (owner.GetViewport().UseHdr2D)
                for (var y = 0; y < image.GetHeight(); y++)
                    for (var x = 0; x < image.GetWidth(); x++) image.SetPixel(x, y, image.GetPixel(x, y).LinearToSrgb());
            image.Convert(Image.Format.Rgba8);
            if (image.SavePng($"{directory}/{label}-{image.GetWidth()}x{image.GetHeight()}.png") != Error.Ok)
                throw new InvalidOperationException("技能图鉴截图保存失败。");
        }
    }

    private static MinimalPlaytest CreateRoot(Control owner)
    {
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        owner.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); return root;
    }

    private static MatchPageViewModel SkillView(IDefinitionCatalog registry)
    {
        var factory = new EntityFactory();
        var session = new CreateMatchService(factory).Create(42, 0, registry.Heroes.Values.First(hero => hero.Attributes.Identity.FactionKey == new StringName("paladin")));
        var acquisition = new SkillAcquisitionService(factory);
        foreach (var definition in registry.Skills.Values) _ = acquisition.AcquireSkill(session, definition, definition.InitialLevel);
        var hidden = new UiAction("", false);
        return new MatchPageViewModel(MatchPage.EncounterChoice, "技能详情验证", "", MatchSnapshot.From(session), null,
            null, false, false, Array.Empty<KeyedAction>(), Array.Empty<KeyedAction>(), Array.Empty<ShopItemViewModel>(),
            Array.Empty<KeyedAction>(), 0, hidden, hidden, hidden, hidden);
    }
}
