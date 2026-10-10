using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Combat;

namespace Project_Star.Presentation.Playtest;

// 只读战斗装饰层；进度取冻结冷却，流光只改变绘制位置。
public sealed partial class CardBattleOverlay : Control
{
    private readonly List<Label> _pills = new();
    private Label _clock = null!;
    private Label _charge = null!;
    private Label _destroyed = null!;
    private CardBattleSnapshot? _state;
    private bool _paused;
    private float _flow;
    private float _flash;
    public float Progress { get; private set; }
    public decimal RemainingSeconds { get; private set; }
    public bool HasTrack { get; private set; }
    public Color ProgressColor { get; private set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _clock = Text("Remaining", 11, new Color("ead5ae"), true);
        foreach (var name in new[] { "Haste", "Slow", "Immobilize", "Flying", "Berserk" })
            _pills.Add(Text(name, 10, Colors.White));
        _charge = Text("Charge", 11, new Color("baf8ef"));
        _charge.HorizontalAlignment = HorizontalAlignment.Center;
        _destroyed = Text("Destroyed", 12, new Color("d7dcd2"));
        _destroyed.Text = "已摧毁"; _destroyed.HorizontalAlignment = HorizontalAlignment.Center;
        Resized += LayoutLayer;
    }

    // 多主动能力沿用最慢冷却比例；无冷却、备战或摧毁不显示发动条。
    public void Render(CardBattleSnapshot? state, bool activated, BattlePlaybackViewModel? playback)
    {
        _state = state; Visible = state is not null && !state.IsOnBench;
        _paused = playback?.Paused ?? true;
        HasTrack = false; Progress = 0; RemainingSeconds = 0; _flash = 0;
        foreach (var pill in _pills) pill.Hide();
        _charge.Hide(); _destroyed.Visible = Visible && state?.Destroyed == true;
        if (!Visible || state is null) { _clock.Hide(); QueueRedraw(); return; }
        var ratio = -1m;
        if (!state.Destroyed)
            for (var index = 0; index < Math.Min(state.CooldownUnits.Count, state.CooldownDurationUnits.Count); index++)
            {
                var duration = state.CooldownDurationUnits[index];
                if (duration <= 0) continue;
                var remaining = Math.Clamp(state.CooldownUnits[index] / duration, 0m, 1m);
                if (remaining < ratio) continue;
                ratio = remaining; RemainingSeconds = Math.Max(0, state.CooldownUnits[index]) / 20m;
                HasTrack = true; Progress = (float)(1 - remaining);
            }
        _clock.Visible = HasTrack;
        _clock.Text = state.Immobilize > 0 ? $"Ⅱ {RemainingSeconds:0.0}s" : $"{RemainingSeconds:0.0}s";
        var haste = state.Haste > 0 && state.Slow == 0;
        var slow = state.Slow > 0 && state.Haste == 0;
        ProgressColor = state.Immobilize > 0 ? AttributePalette.Find(AttributePalette.Immobilize)!.Value
            : haste ? AttributePalette.Find(AttributePalette.Haste)!.Value : slow ? AttributePalette.Find(AttributePalette.Slow)!.Value : new Color("fae3ab");
        if (!state.Destroyed)
        {
            Pill(0, state.Haste > 0, $"↑ {state.Haste / 10m:0.0}s", "66d9e8");
            Pill(1, state.Slow > 0, $"↓ {state.Slow / 10m:0.0}s", "c0aaa0");
            Pill(2, state.Immobilize > 0, $"Ⅱ {state.Immobilize / 10m:0.0}s", "e2b8e8");
            Pill(3, state.IsFlying, "飞行", "c4e9dc");
            Pill(4, state.IsBerserk, "狂暴", "f0b996");
            if (activated) _flash = 1;
            if (playback is not null)
            {
                if (playback.Activations.TryGetValue(state.Id, out var tick))
                    _flash = Mathf.Clamp(1 - (playback.Tick - tick) / 5f, 0, 1);
                var charged = playback.VisualEvents.OfType<CardChargedEvent>()
                    .Where(item => item.TargetCardId == state.Id && playback.Tick - item.Tick.Value <= 8).ToArray();
                if (charged.Length > 0)
                {
                    var latest = charged.Max(item => item.Tick.Value);
                    _charge.Text = $"充能 +{charged.Where(item => item.Tick.Value == latest).Sum(item => item.AmountTicks) / 10m:0.0}s";
                    _charge.Visible = true;
                    _charge.Modulate = new Color(1, 1, 1, Mathf.Clamp(1 - (playback.Tick - latest) / 9f, 0, 1));
                }
            }
        }
        LayoutLayer(); QueueRedraw();
    }

    private Label Text(string name, int size, Color color, bool serif = false)
    {
        var label = new Label { Name = name, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        MatchTheme.Text(label, size, color, serif);
        label.AddThemeConstantOverride("outline_size", 2);
        label.AddThemeColorOverride("font_outline_color", new Color("071513"));
        AddChild(label); return label;
    }
    private void Pill(int index, bool visible, string text, string color)
    {
        var pill = _pills[index]; pill.Visible = visible; pill.Text = text;
        MatchTheme.Text(pill, 10, new Color(color));
        var plate = MatchTheme.Surface(new Color("0d221ce6"), new Color(color));
        plate.ContentMarginLeft = plate.ContentMarginRight = 3;
        plate.ContentMarginTop = plate.ContentMarginBottom = 1;
        pill.AddThemeStyleboxOverride("normal", plate);
    }
    private void LayoutLayer()
    {
        if (_clock is null) return;
        _clock.Size = _clock.GetCombinedMinimumSize(); _clock.Position = new Vector2(Size.X - _clock.Size.X - 6, 46);
        var left = 6f; var top = 47f;
        foreach (var pill in _pills.Where(item => item.Visible))
        {
            pill.Size = pill.GetCombinedMinimumSize();
            var limit = top < 64 ? Size.X - _clock.Size.X - 12 : Size.X - 6;
            if (left + pill.Size.X > limit && left > 6 || left == 6 && left + pill.Size.X > limit && top < 64)
            { left = 6; top += 19; }
            pill.Position = new Vector2(left, top); left += pill.Size.X + 3;
        }
        _charge.Position = new Vector2(5, top + 22); _charge.Size = new Vector2(Mathf.Max(1, Size.X - 10), 19);
        _destroyed.Position = new Vector2(3, Size.Y * .44f); _destroyed.Size = new Vector2(Mathf.Max(1, Size.X - 6), 24);
    }
    public override void _Process(double delta)
    {
        if (!Visible || _paused || _state?.Destroyed != false) return;
        _flow = (_flow + (float)delta / .8f) % 1; QueueRedraw();
    }
    public override void _Draw()
    {
        if (_state is null || !Visible) return;
        if (HasTrack)
        {
            var track = new Rect2(6, 35, Mathf.Max(1, Size.X - 12), 8);
            var plate = MatchTheme.Surface(new Color("071513dc"), new Color("ad987c77")); plate.SetCornerRadiusAll(4);
            DrawStyleBox(plate, track);
            var fill = new Rect2(track.Position + Vector2.One, new Vector2(Mathf.Max(0, (track.Size.X - 2) * Progress), 6));
            if (fill.Size.X > 0)
            {
                for (var x = 0; x < (int)Math.Ceiling(fill.Size.X); x++)
                {
                    var color = ProgressColor.Darkened(.45f).Lerp(ProgressColor, x / Mathf.Max(1, fill.Size.X));
                    DrawRect(new Rect2(fill.Position + new Vector2(x, 0), new Vector2(Mathf.Min(1, fill.Size.X - x), 6)), color);
                }
                if (_state.Slow > 0 && _state.Haste == 0 && _state.Immobilize == 0)
                    for (var x = -6f; x < fill.Size.X + 6; x += 12)
                    {
                        if (x + 6 <= 0 || x - 4 >= fill.Size.X) continue;
                        var points = new[] { fill.Position + new Vector2(Mathf.Clamp(x, 0, fill.Size.X), 0),
                            fill.Position + new Vector2(Mathf.Clamp(x + 6, 0, fill.Size.X), 0),
                            fill.Position + new Vector2(Mathf.Clamp(x + 2, 0, fill.Size.X), 6),
                            fill.Position + new Vector2(Mathf.Clamp(x - 4, 0, fill.Size.X), 6) }.Distinct().ToArray();
                        if (points.Length >= 3) DrawColoredPolygon(points, new Color(ProgressColor.Darkened(.32f), .75f));
                    }
                if (_state.Immobilize == 0)
                    DrawLine(fill.Position + new Vector2(fill.Size.X, 0), fill.End, new Color(ProgressColor, .85f), 2);
                if (_state.Haste > 0 && _state.Slow == 0 && _state.Immobilize == 0)
                    DrawRect(new Rect2(fill.Position + new Vector2(fill.Size.X * _flow, 0), new Vector2(Mathf.Min(5, fill.Size.X * (1 - _flow)), 6)), new Color("e0fff180"));
            }
        }
        if (_state.Immobilize > 0 && !_state.Destroyed)
        {
            var color = new Color("b0d6eb99"); var y = Size.Y * .5f;
            DrawLine(new Vector2(Size.X * .09f, y + 10), new Vector2(Size.X * .91f, y - 10), color, 3);
            DrawLine(new Vector2(Size.X * .09f, y + 16), new Vector2(Size.X * .91f, y - 4), color, 3);
            for (var x = Size.X * .09f; x < Size.X * .91f; x += 12)
            { var offset = 10 - 20 * (x / Size.X - .09f) / .82f; DrawLine(new Vector2(x, y + offset), new Vector2(x + 3, y + offset + 6), color, 1); }
            DrawRect(new Rect2(Size.X / 2 - 9, y - 8, 18, 18), new Color("172630dd"));
            DrawRect(new Rect2(Size.X / 2 - 9, y - 8, 18, 18), color, false, 2);
        }
        if (_flash > 0)
        {
            var glow = MatchTheme.Outline(new Color(new Color("fff0ba"), _flash * .65f)); glow.SetBorderWidthAll(4);
            DrawStyleBox(glow, new Rect2(Vector2.One * 4, Size - Vector2.One * 8));
        }
        if (_state.Destroyed) DrawRect(new Rect2(3, Size.Y * .44f, Mathf.Max(1, Size.X - 6), 24), new Color("101719bb"));
    }
    public override void _ExitTree() => Resized -= LayoutLayer;
}
