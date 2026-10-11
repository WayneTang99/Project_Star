using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Playtest;

// 宝石商店只读取报价快照，拖拽预览和提交交给应用用例。
public sealed partial class GemShopView : Control
{
    public Func<BoardDragData, int, long, Result>? PreviewRequested { get; set; }
    public event Action<BoardDragData, int, long>? DropRequested;
    private HBoxContainer _anvils = null!;
    private Label _hint = null!;
    public override void _Ready()
    {
        _hint = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        MatchTheme.Text(_hint, 13, MatchTheme.Muted); AddChild(_hint);
        _anvils = new HBoxContainer { Name = "Anvils", SizeFlagsVertical = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        _anvils.AddThemeConstantOverride("separation", 18); AddChild(_anvils);
        Resized += Layout; Layout();
    }
    private void Layout()
    {
        _hint.Position = Vector2.Zero; _hint.Size = new Vector2(Size.X, 24);
        _anvils.Position = new Vector2(0, 30); _anvils.Size = new Vector2(Size.X, Math.Max(0, Size.Y - 30));
    }
    public void Render(MatchSnapshot? player, IReadOnlyList<GemAnvilViewModel> offers)
    {
        _hint.Text = offers.Count > 0 && offers[0].Resolved ? "已完成镶嵌 · 仍可拖到左侧英雄区出售" : "拖到右侧砧板镶嵌 · 拖到左侧英雄区出售";
        foreach (var child in _anvils.GetChildren()) { _anvils.RemoveChild(child); child.QueueFree(); }
        foreach (var offer in offers)
        {
            var anvil = new GemAnvilDropZone { Name = $"Anvil{offer.Index}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _anvils.AddChild(anvil); anvil.Render(player, offer);
            anvil.PreviewRequested = (drag, index, revision) => PreviewRequested?.Invoke(drag, index, revision)
                ?? Result.Fail(new Failure(new StringName("ui.gem_unavailable"), "镶嵌暂不可用。"));
            anvil.DropRequested += (drag, index, revision) => DropRequested?.Invoke(drag, index, revision);
        }
        Layout();
    }
}
