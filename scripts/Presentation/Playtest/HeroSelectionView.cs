using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Project_Star.Presentation.Playtest;

// 选角横向浏览器；切换只改变展示，确认才提交英雄key（表现层）。
public sealed partial class HeroSelectionView : Control
{
    public event Action<StringName, long>? Selected;
    private readonly TextureRect _left = Portrait("PreviousPortrait");
    private readonly TextureRect _center = Portrait("CurrentPortrait");
    private readonly TextureRect _right = Portrait("NextPortrait");
    private readonly Label _message = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Label _name = new() { Name = "HeroName", HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _previous = new() { Name = "Previous", Text = "←" };
    private readonly Button _next = new() { Name = "Next", Text = "→" };
    private readonly Button _choose = new() { Name = "Choose", Text = "选择英雄" };
    private IReadOnlyList<KeyedAction> _heroes = Array.Empty<KeyedAction>();
    private int _index;
    private long _revision;

    public override void _Ready()
    {
        foreach (var control in new Control[] { _left, _center, _right, _message, _name, _previous, _next, _choose }) AddChild(control);
        _previous.Pressed += Previous; _next.Pressed += Next; _choose.Pressed += Choose;
        Resized += Layout; Layout();
    }

    // 同页刷新保留正在浏览的英雄；列表变动时仍按key定位。
    public void Render(string message, IReadOnlyList<KeyedAction> heroes, long revision = 0)
    {
        var key = _heroes.Count == 0 ? new StringName("") : _heroes[_index].Key;
        _heroes = Array.AsReadOnly(heroes.Where(hero => hero.Action.Visible).ToArray());
        _index = Math.Max(0, Array.FindIndex(_heroes.ToArray(), hero => hero.Key == key));
        _revision = revision; _message.Text = message; UpdatePortraits();
    }

    public override void _ExitTree()
    {
        _previous.Pressed -= Previous; _next.Pressed -= Next; _choose.Pressed -= Choose; Resized -= Layout;
    }

    private void Previous() => Move(-1);
    private void Next() => Move(1);
    private void Move(int direction)
    {
        if (_heroes.Count < 2) return;
        _index = (_index + direction + _heroes.Count) % _heroes.Count; UpdatePortraits();
    }
    private void Choose()
    {
        if (_heroes.Count > 0 && !_choose.Disabled && Visible) Selected?.Invoke(_heroes[_index].Key, _revision);
    }
    private void UpdatePortraits()
    {
        _previous.Disabled = _next.Disabled = _heroes.Count < 2;
        _left.Visible = _right.Visible = _heroes.Count > 1;
        _choose.Disabled = _heroes.Count == 0 || !_heroes[_index].Action.Enabled;
        _center.Texture = _heroes.Count == 0 ? null : Load(_heroes[_index]);
        _name.Text = _heroes.Count == 0 ? "暂无英雄" : _heroes[_index].Action.Text.Replace("选择：", "");
        if (_heroes.Count > 1)
        {
            _left.Texture = Load(_heroes[(_index - 1 + _heroes.Count) % _heroes.Count]);
            _right.Texture = Load(_heroes[(_index + 1) % _heroes.Count]);
        }
    }
    private void Layout()
    {
        var main = Mathf.Max(1, Mathf.Min(Size.Y - 125, Size.X * .42f));
        var side = main * .55f; var gap = 24f;
        _message.Position = Vector2.Zero; _message.Size = new Vector2(Size.X, 30);
        _center.Position = new Vector2((Size.X - main) / 2, 38); _center.Size = Vector2.One * main;
        _left.Position = new Vector2(_center.Position.X - side - gap, 38); _left.Size = Vector2.One * side;
        _right.Position = new Vector2(_center.Position.X + main + gap, 38); _right.Size = Vector2.One * side;
        _previous.Position = _left.Position + new Vector2((side - 64) / 2, side + 20); _previous.Size = new Vector2(64, 44);
        _next.Position = _right.Position + new Vector2((side - 64) / 2, side + 20); _next.Size = new Vector2(64, 44);
        _name.Position = new Vector2(_center.Position.X, main + 44); _name.Size = new Vector2(main, 28);
        _choose.Position = new Vector2(_center.Position.X, main + 77); _choose.Size = new Vector2(main, 40);
    }
    private static TextureRect Portrait(string name) => new() { Name = name, MouseFilter = MouseFilterEnum.Ignore,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
    private static Texture2D? Load(KeyedAction hero) => !hero.Illustration.IsEmpty && ResourceLoader.Exists(hero.Illustration.ToString())
        ? GD.Load<Texture2D>(hero.Illustration.ToString()) : null;
}
