using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 统一界面的应用门控与原生键盘检查，不把样例数据加入正式内容。
internal static class UnifiedUiChecks
{
    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    // 菜单打开／取消只读；整局放弃结束旧聚合，过期确认和交易不能影响新局。
    internal static bool MenuAndAbandon(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<Control>(); owner.AddChild(scene);
        try
        {
            var presenter = Field<MatchPresenter>(scene, "_presenter"); presenter.SelectHero(new StringName("hero.paladin"));
            var session = Field<MatchSession>(presenter, "_player");
            var registry = DefinitionRegistry.Scan(typeof(UnifiedUiChecks).Assembly);
            var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
            var card = new CardEconomyService(new EntityFactory(), board, registry.Cards.Values)
                .AcquireAndPlace(session, new BoarCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
            presenter.RefreshView();
            var shell = scene.GetNode<MatchShell>("MatchShell"); var toggle = shell.GetNode<Button>("OpenGameMenu");
            var menu = shell.GetNode<MatchMenuView>("GameMenu");
            Button Entry(string name) => menu.GetNode<Button>("ItemsPanel/Items/" + name);
            var wealth = session.Player.Wealth; var random = session.Random.State; var events = session.Events.Count;
            toggle.EmitSignal(Button.SignalName.Pressed);
            if (!menu.Visible || Entry("Abandon").Disabled || Entry("Abandon").FindNextValidFocus() != Entry("Resume")) return Fail("菜单门控或焦点循环");
            Entry("OpenCardCatalog").EmitSignal(Button.SignalName.Pressed);
            var catalog = shell.GetNode<CardCatalogView>("CardCatalog");
            if (menu.Visible || !catalog.Visible) return Fail("局内卡牌图鉴");
            presenter.RefreshView();
            if (!catalog.Visible) return Fail("刷新保持图鉴");
            catalog.Close();
            if (!toggle.HasFocus()) return Fail("图鉴返回焦点");
            toggle.EmitSignal(Button.SignalName.Pressed); Entry("OpenSkillCatalog").EmitSignal(Button.SignalName.Pressed);
            shell.GetNode<SkillCatalogView>("SkillCatalog").Close();
            toggle.EmitSignal(Button.SignalName.Pressed); Entry("Abandon").EmitSignal(Button.SignalName.Pressed);
            var confirmation = menu.GetNode<PanelContainer>("AbandonConfirmation");
            var cancel = confirmation.FindChildren("CancelAbandon", "Button", true, false).OfType<Button>().Single();
            var confirm = confirmation.FindChildren("ConfirmAbandon", "Button", true, false).OfType<Button>().Single();
            if (!confirmation.Visible || !cancel.HasFocus()) return Fail("二次确认");
            cancel.EmitSignal(Button.SignalName.Pressed);
            if (menu.Visible || session.Status != MatchStatus.InProgress || session.Player.Wealth != wealth
                || session.Random.State != random || session.Events.Count != events || !session.Board.Contains(card.Id)) return Fail("取消只读");
            toggle.EmitSignal(Button.SignalName.Pressed); Entry("Abandon").EmitSignal(Button.SignalName.Pressed); confirm.EmitSignal(Button.SignalName.Pressed);
            if (session.Status != MatchStatus.Lost || session.Summary?.FinalWealth != wealth || presenter.View.Player is not null
                || presenter.View.Page != MatchPage.HeroSelection || menu.Visible || session.Random.State != random) return Fail("结束整局");
            presenter.SelectHero(new StringName("hero.paladin")); var fresh = presenter.View.Player!;
            presenter.AbandonMatch(session.Id); presenter.SellDraggedCard(session.Id, card.Id, 1, 4); presenter.AdvancePlayback(100);
            return presenter.View.Player?.MatchId == fresh.MatchId && presenter.View.Player.Status == MatchStatus.InProgress
                && presenter.View.Player.Wealth == fresh.Wealth;
        }
        finally { owner.RemoveChild(scene); scene.Free(); }
        static bool Fail(string reason) { GD.Print($"统一菜单检查失败：{reason}"); return false; }
    }

    // 相同属性在两主题保持同色，正文的属性数值不能串色或被解释为BBCode。
    internal static bool AttributeText(Control owner)
    {
        var label = new RichTextLabel(); owner.AddChild(label);
        var previous = MatchTheme.BluePalette;
        try
        {
            const string text = "获得护甲10，造成10点普通伤害，治疗5，最大生命+20；出售20件植物卡牌。疾速0.5秒 [url]";
            foreach (var blue in new[] { false, true })
            {
                MatchTheme.SetPalette(blue); CardKeywordText.RenderRule(label, text);
                if (label.GetParsedText() != text || AttributePalette.Find(GameAttributeKeys.Armor) != new Color("8ccbff")
                    || AttributePalette.Find(GameAttributeKeys.HealingBonus) != new Color("7ad99b")) return false;
                var tokens = new System.Text.RegularExpressions.Regex("护甲|普通伤害|治疗|最大生命|疾速|(?<number>[0-9]+(?:\\.[0-9]+)?)").Matches(text)
                    .Cast<System.Text.RegularExpressions.Match>().ToArray();
                var numbers = tokens.Where(token => token.Groups["number"].Success).ToArray();
                if (CardKeywordText.NumberColor(text, numbers[0], tokens) != new Color("8ccbff")
                    || CardKeywordText.NumberColor(text, numbers[1], tokens) != new Color("ff9c83")
                    || CardKeywordText.NumberColor(text, numbers[4], tokens) is not null) return false;
            }
            return true;
        }
        finally { MatchTheme.SetPalette(previous); owner.RemoveChild(label); label.Free(); }
    }

    internal static async Task<bool> NativeMenu(Control owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1600, 900), Disable3D = true, HandleInputLocally = true };
        owner.AddChild(viewport);
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<Control>(); viewport.AddChild(scene);
        try
        {
            var presenter = Field<MatchPresenter>(scene, "_presenter"); presenter.SelectHero(new StringName("hero.paladin"));
            var session = Field<MatchSession>(presenter, "_player"); var wealth = session.Player.Wealth; var random = session.Random.State;
            var shell = scene.GetNode<MatchShell>("MatchShell"); var toggle = shell.GetNode<Button>("OpenGameMenu");
            var menu = shell.GetNode<MatchMenuView>("GameMenu");
            Button Entry(string name) => menu.GetNode<Button>("ItemsPanel/Items/" + name);
            await Frame(); toggle.GrabFocus(); await KeyInput(Key.Enter);
            if (!menu.Visible || !Entry("Resume").HasFocus()) return Fail("Enter打开菜单");
            for (var i = 0; i < 5; i++) await KeyInput(Key.Tab);
            if (!Entry("Resume").HasFocus()) return Fail("Tab循环");
            await KeyInput(Key.Tab, shift: true);
            if (!Entry("Abandon").HasFocus()) return Fail("反向Tab循环");
            await KeyInput(Key.Escape);
            if (menu.Visible || !toggle.HasFocus()) return Fail("Escape返回");
            await KeyInput(Key.Enter); await KeyInput(Key.Tab); await KeyInput(Key.Enter);
            var settings = shell.GetNode<DisplaySettingsView>("DisplaySettings");
            if (!settings.Visible) return Fail("局内设置");
            await KeyInput(Key.Escape);
            if (settings.Visible || !toggle.HasFocus()) return Fail("设置取消焦点");
            await KeyInput(Key.Enter); await KeyInput(Key.Tab); await KeyInput(Key.Tab); await KeyInput(Key.Enter);
            var catalog = shell.GetNode<CardCatalogView>("CardCatalog");
            if (!catalog.Visible || !catalog.GetNode<LineEdit>("Content/Filters/Search").HasFocus()) return Fail("局内图鉴");
            var back = catalog.GetNode<Button>("Content/Header/Back"); back.GrabFocus(); await KeyInput(Key.Tab, shift: true);
            if (viewport.GuiGetFocusOwner() is not { } focus || !catalog.IsAncestorOf(focus)) return Fail("图鉴焦点不能泄漏");
            await KeyInput(Key.Escape);
            if (catalog.Visible || !toggle.HasFocus()) return Fail("图鉴返回");
            await KeyInput(Key.Enter);
            for (var i = 0; i < 4; i++) await KeyInput(Key.Tab);
            await KeyInput(Key.Enter); await KeyInput(Key.Escape);
            if (session.Status != MatchStatus.InProgress || session.Player.Wealth != wealth || session.Random.State != random) return Fail("取消放弃只读");
            await KeyInput(Key.Enter);
            for (var i = 0; i < 4; i++) await KeyInput(Key.Tab);
            await KeyInput(Key.Enter); await KeyInput(Key.Tab); await KeyInput(Key.Enter);
            return presenter.View.Page == MatchPage.HeroSelection && session.Status == MatchStatus.Lost && !menu.Visible;

            async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            async Task KeyInput(Key key, bool shift = false)
            {
                viewport.PushInput(new InputEventKey { Pressed = true, Keycode = key, PhysicalKeycode = key, ShiftPressed = shift }, true); await Frame();
                viewport.PushInput(new InputEventKey { Pressed = false, Keycode = key, PhysicalKeycode = key, ShiftPressed = shift }, true); await Frame();
            }
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
        static bool Fail(string reason) { GD.Print($"原生菜单检查失败：{reason}"); return false; }
    }
}
