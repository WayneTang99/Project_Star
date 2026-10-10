using System;
using System.Linq;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 模态菜单门控棋盘输入；确认携带打开时的对局身份，按钮不接触模型。
public sealed partial class MatchMenuView : Control
{
    public event Action? SettingsRequested;
    public event Action? CardsRequested;
    public event Action? SkillsRequested;
    public event Action<Guid>? AbandonRequested;
    private PanelContainer _items = null!;
    private PanelContainer _confirmation = null!;
    private Button _abandon = null!;
    private Button _resume = null!;
    private Button _cancel = null!;
    private Guid _matchId;
    private bool _canAbandon;
    private Control? _returnFocus;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        var shade = new ColorRect { Name = "Shade", Color = new Color(0, 0, 0, .35f) };
        AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shade.GuiInput += Outside;
        _items = Panel("ItemsPanel");
        var entries = new VBoxContainer { Name = "Items" }; _items.AddChild(entries);
        var heading = new Label { Text = "星辰之旅" }; MatchTheme.Text(heading, 16, MatchTheme.Gold, spacing: 2); entries.AddChild(heading);
        _resume = Entry(entries, "Resume", "继续游戏", Close);
        Entry(entries, "OpenDisplaySettings", "设置", () => { Close(); SettingsRequested?.Invoke(); });
        Entry(entries, "OpenCardCatalog", "卡牌图鉴", () => { Close(); CardsRequested?.Invoke(); });
        Entry(entries, "OpenSkillCatalog", "技能图鉴", () => { Close(); SkillsRequested?.Invoke(); });
        _abandon = Entry(entries, "Abandon", "放弃对局", ConfirmAbandon);
        _confirmation = Panel("AbandonConfirmation");
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 18); _confirmation.AddChild(column);
        var title = new Label { Text = "放弃对局？" }; MatchTheme.Text(title, 24, MatchTheme.Gold); column.AddChild(title);
        column.AddChild(new Label { Text = "当前旅程将结束，返回英雄选择。", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        var buttons = new HBoxContainer(); column.AddChild(buttons);
        _cancel = Entry(buttons, "CancelAbandon", "继续旅程", Close);
        var confirm = Entry(buttons, "ConfirmAbandon", "确认放弃", () => { var id = _matchId; Close(); if (_canAbandon) AbandonRequested?.Invoke(id); });
        MatchTheme.Accent(confirm);
        Loop(_cancel, confirm);
        Resized += LayoutMenu; Hide(); LayoutMenu();
    }
    private PanelContainer Panel(string name)
    {
        var panel = new PanelContainer { Name = name };
        var style = MatchTheme.Surface(MatchTheme.SurfaceColor, MatchTheme.Gold);
        style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 18;
        panel.AddThemeStyleboxOverride("panel", style); AddChild(panel); return panel;
    }
    private static Button Entry(BoxContainer parent, string name, string title, Action action)
    {
        var button = new Button { Name = name, Text = title, CustomMinimumSize = new Vector2(0, 38), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(button); button.Pressed += action; return button;
    }
    internal static void Loop(params Control[] controls)
    {
        for (var i = 0; i < controls.Length; i++)
        {
            controls[i].FocusNext = controls[i].GetPathTo(controls[(i + 1) % controls.Length]);
            controls[i].FocusPrevious = controls[i].GetPathTo(controls[(i + controls.Length - 1) % controls.Length]);
        }
    }
    public void Open(Guid matchId, bool canAbandon, Control returnFocus)
    {
        RefreshPalette();
        _matchId = matchId; _canAbandon = canAbandon; _returnFocus = returnFocus;
        _abandon.Disabled = !canAbandon; _abandon.TooltipText = canAbandon ? "" : "当前没有进行中的对局。";
        _confirmation.Hide(); _items.Show();
        Loop(_items.GetNode<VBoxContainer>("Items").GetChildren().OfType<Button>().Where(button => !button.Disabled).Cast<Control>().ToArray());
        Show(); LayoutMenu(); _resume.GrabFocus();
    }
    public void Close()
    {
        Hide();
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus();
    }
    private void ConfirmAbandon()
    {
        if (!_canAbandon) return;
        _items.Hide(); _confirmation.Show(); LayoutMenu(); _cancel.GrabFocus();
    }
    // 模态图鉴不让Tab移入被覆盖的棋盘或选角控件。
    internal static void TrapTab(Control modal, InputEventKey key)
    {
        if (key.Keycode != Key.Tab) return;
        var controls = modal.FindChildren("*", "Control", true, false).OfType<Control>()
            .Where(control => control.IsVisibleInTree() && control.FocusMode == FocusModeEnum.All
                && (control is not BaseButton button || !button.Disabled)).ToArray();
        if (controls.Length == 0) return;
        var focus = modal.GetViewport().GuiGetFocusOwner();
        var next = key.ShiftPressed ? focus?.FindPrevValidFocus() : focus?.FindNextValidFocus();
        if (next is null || !modal.IsAncestorOf(next)) next = key.ShiftPressed ? controls[^1] : controls[0];
        next.GrabFocus(); modal.GetViewport().SetInputAsHandled();
    }
    public void RefreshPalette()
    {
        foreach (var panel in new[] { _items, _confirmation })
        {
            var style = MatchTheme.Plate(MatchTheme.BluePalette ? "blue-surface" : "surface");
            style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 18;
            panel.AddThemeStyleboxOverride("panel", style);
        }
    }
    private void Outside(InputEvent input)
    { if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) Close(); }
    private void LayoutMenu()
    {
        _items.Size = new Vector2(260, 0); _items.Size = _items.GetCombinedMinimumSize().Max(new Vector2(260, 0));
        _items.Position = new Vector2(Size.X - 24 - _items.Size.X, 66);
        _confirmation.Size = new Vector2(440, 0); _confirmation.Size = _confirmation.GetCombinedMinimumSize().Max(new Vector2(440, 0));
        _confirmation.Position = (Size - _confirmation.Size) / 2;
    }
    public override void _Input(InputEvent input)
    {
        if (!Visible || input is not InputEventKey { Pressed: true } key) return;
        if (key.Keycode == Key.Escape) { Close(); GetViewport().SetInputAsHandled(); }
        else if (key.Keycode is Key.Up or Key.Down)
        {
            var focus = GetViewport().GuiGetFocusOwner();
            if (focus is not null && IsAncestorOf(focus))
                (key.Keycode == Key.Down ? focus.FindNextValidFocus() : focus.FindPrevValidFocus())?.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _ExitTree() => Resized -= LayoutMenu;
}
