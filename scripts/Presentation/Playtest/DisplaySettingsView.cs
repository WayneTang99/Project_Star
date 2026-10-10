using System;
using System.Linq;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 表现层显示设置；预览窗口分辨率，确认后写入本地配置，取消或超时恢复。
public sealed partial class DisplaySettingsView : Control
{
    private static readonly Vector2I[] Resolutions = [new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440), new(2560, 1080)];
    public event Action<bool>? PaletteChanged;
    internal string ConfigurationPath { get; set; } = "user://display.cfg";
    internal Window? TargetWindow { get; set; }
    private Window _window = null!;
    private PanelContainer _panel = null!;
    private OptionButton _resolution = null!;
    private OptionButton _palette = null!;
    private Label _message = null!;
    private Button _apply = null!;
    private Button _confirm = null!;
    private Button _cancel = null!;
    private Vector2I _previousSize;
    private Vector2I _previousPosition;
    private bool _previousPalette;
    private bool _preview;
    private double _remaining;
    private Control? _returnFocus;

    public override void _Ready()
    {
        _window = TargetWindow ?? GetWindow(); Theme = MatchTheme.Create();
        var shade = new ColorRect { Color = new Color(0, 0, 0, .65f) }; AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _panel = new PanelContainer(); _panel.AddThemeStyleboxOverride("panel", MatchTheme.Surface(MatchTheme.SurfaceColor, MatchTheme.Gold)); AddChild(_panel);
        var margin = new MarginContainer(); _panel.AddChild(margin);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 24);
        var column = new VBoxContainer(); column.AddThemeConstantOverride("separation", 16); margin.AddChild(column);
        var title = new Label { Text = "显示设置" }; MatchTheme.Text(title, 24, MatchTheme.Gold, true); column.AddChild(title);
        column.AddChild(new Label { Text = "分辨率 · 画面保持 16:9" });
        _resolution = new OptionButton { Name = "Resolution" }; column.AddChild(_resolution);
        foreach (var size in Resolutions) _resolution.AddItem($"{size.X} × {size.Y}");
        column.AddChild(new Label { Text = "桌面配色" });
        _palette = new OptionButton { Name = "Palette" }; _palette.AddItem("森林金"); _palette.AddItem("星辉蓝"); column.AddChild(_palette);
        _message = new Label { Name = "Message", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(400, 44) };
        column.AddChild(_message);
        var buttons = new HBoxContainer(); column.AddChild(buttons);
        _apply = new Button { Name = "Apply", Text = "应用", SizeFlagsHorizontal = SizeFlags.ExpandFill }; MatchTheme.Accent(_apply);
        _confirm = new Button { Name = "Confirm", Text = "保留设置", SizeFlagsHorizontal = SizeFlags.ExpandFill }; MatchTheme.Accent(_confirm);
        _cancel = new Button { Name = "Cancel", Text = "取消", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        buttons.AddChild(_apply); buttons.AddChild(_confirm); buttons.AddChild(_cancel);
        _apply.Pressed += Preview; _confirm.Pressed += Confirm; _cancel.Pressed += Cancel;
        Resized += LayoutPanel; VisibilityChanged += VisibilityUpdated; Hide(); LayoutPanel();
    }

    // 启动时只读取已确认设置；无效配置沿用当前窗口，不写入对局。
    public void LoadSaved()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigurationPath) != Error.Ok) return;
        var size = new Vector2I((int)config.GetValue("display", "width", 0), (int)config.GetValue("display", "height", 0));
        if (Resolutions.Contains(size) && FitsScreen(size)) SetSize(size);
        SetPalette((bool)config.GetValue("display", "blue", false));
    }

    // 从菜单打开，原焦点在关闭后恢复。
    public void Open()
    {
        if (Visible) return;
        _returnFocus = GetViewport().GuiGetFocusOwner();
        _resolution.Select(Math.Max(0, Array.IndexOf(Resolutions, _window.Size)));
        _palette.Select(MatchTheme.BluePalette ? 1 : 0);
        _message.Text = "应用后请在 15 秒内确认；取消或超时恢复原分辨率与配色。";
        _preview = false; _resolution.Disabled = _palette.Disabled = false; _apply.Show(); _confirm.Hide(); Show(); _resolution.GrabFocus();
        FocusLoop(_resolution, _palette, _apply, _cancel);
    }

    private bool FitsScreen(Vector2I size)
    {
        if (DisplayServer.GetName() == "headless") return true;
        var available = ScreenBounds().Size;
        return size.X <= available.X && size.Y <= available.Y;
    }
    private Rect2I ScreenBounds() => _window.Borderless
        ? new Rect2I(DisplayServer.ScreenGetPosition(_window.CurrentScreen), DisplayServer.ScreenGetSize(_window.CurrentScreen))
        : DisplayServer.ScreenGetUsableRect(_window.CurrentScreen);
    private void SetSize(Vector2I size)
    {
        _window.Size = size;
        if (TargetWindow is null)
        {
            var screen = ScreenBounds();
            _window.Position = screen.Position + (screen.Size - size) / 2;
        }
    }
    private void Preview()
    {
        if (_preview) return;
        var size = Resolutions[_resolution.Selected];
        if (!FitsScreen(size)) { _message.Text = "此分辨率超出当前屏幕可用范围，请选择较小尺寸。"; return; }
        _previousSize = _window.Size; _previousPosition = _window.Position;
        _previousPalette = MatchTheme.BluePalette;
        _preview = true; _remaining = 15; SetSize(size);
        SetPalette(_palette.Selected == 1);
        _resolution.Disabled = _palette.Disabled = true; _apply.Hide(); _confirm.Show(); UpdateCountdown(); _confirm.GrabFocus();
        FocusLoop(_confirm, _cancel);
    }
    private void Confirm()
    {
        if (!_preview) return;
        var config = new ConfigFile(); config.SetValue("display", "width", _window.Size.X); config.SetValue("display", "height", _window.Size.Y);
        config.SetValue("display", "blue", MatchTheme.BluePalette);
        if (config.Save(ConfigurationPath) != Error.Ok)
        {
            Restore(); _message.Text = "设置保存失败，已恢复原分辨率和配色。"; _apply.Show(); _confirm.Hide(); _resolution.Disabled = _palette.Disabled = false;
            FocusLoop(_resolution, _palette, _apply, _cancel); _apply.GrabFocus(); return;
        }
        _preview = false; Hide(); RestoreFocus();
    }
    private void Restore()
    { if (_preview) { _window.Size = _previousSize; _window.Position = _previousPosition; _preview = false; SetPalette(_previousPalette); } }
    private void SetPalette(bool blue)
    {
        MatchTheme.SetPalette(blue); Theme = MatchTheme.Create();
        _panel.AddThemeStyleboxOverride("panel", MatchTheme.Plate(blue ? "blue-surface" : "surface"));
        MatchTheme.Accent(_apply); MatchTheme.Accent(_confirm); PaletteChanged?.Invoke(blue);
    }
    private void Cancel() { Restore(); Hide(); RestoreFocus(); }
    private void RestoreFocus() { if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus(); }
    private void VisibilityUpdated() { if (!Visible) Restore(); }
    private static void FocusLoop(params Control[] controls)
    {
        for (var index = 0; index < controls.Length; index++)
        {
            var control = controls[index];
            control.FocusNext = control.GetPathTo(controls[(index + 1) % controls.Length]);
            control.FocusPrevious = control.GetPathTo(controls[(index + controls.Length - 1) % controls.Length]);
        }
    }
    private void UpdateCountdown() => _message.Text = $"是否保留此分辨率？{Math.Ceiling(_remaining)} 秒后恢复原设置。";
    private void LayoutPanel() { _panel.Size = _panel.GetCombinedMinimumSize(); _panel.Position = (Size - _panel.Size) / 2; }
    public override void _Process(double delta)
    {
        if (!_preview) return;
        _remaining -= delta;
        if (_remaining <= 0) Cancel(); else UpdateCountdown();
    }
    public override void _Input(InputEvent input)
    {
        if (!Visible || input is not InputEventKey { Pressed: true, Keycode: Key.Escape }) return;
        Cancel(); GetViewport().SetInputAsHandled();
    }
    public override void _ExitTree()
    { Restore(); Resized -= LayoutPanel; VisibilityChanged -= VisibilityUpdated; _apply.Pressed -= Preview; _confirm.Pressed -= Confirm; _cancel.Pressed -= Cancel; }
}
