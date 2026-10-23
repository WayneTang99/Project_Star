using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Match;

namespace Project_Star.Presentation.Game;

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
        _hint.Position = new Vector2(0, 18); _hint.Size = new Vector2(Size.X, 18);
        _hint.AddThemeColorOverride("font_color", new Color("51412f"));
        _anvils.Position = new Vector2(0, 48); _anvils.Size = new Vector2(Size.X, 156);
    }
    // 全局拖拽反馈按真实砧板矩形分派，失败不能退化为出售。
    internal GemAnvilDropZone? TargetAt(Vector2 globalPoint) => _anvils.GetChildren().OfType<GemAnvilDropZone>()
        .FirstOrDefault(anvil => anvil.IsVisibleInTree() && anvil.GetGlobalRect().HasPoint(globalPoint));

    public void Render(MatchSnapshot? player, IReadOnlyList<GemAnvilViewModel> offers)
    {
        _hint.Text = offers.Count > 0 && offers[0].Resolved ? "已完成镶嵌" : "拖动卡牌至宝石镶嵌 · 移出棋盘出售";
        foreach (var child in _anvils.GetChildren()) { _anvils.RemoveChild(child); child.QueueFree(); }
        foreach (var offer in offers)
        {
            var anvil = new GemAnvilDropZone { Name = $"Anvil{offer.Index}", CustomMinimumSize = new Vector2(170, 156) };
            _anvils.AddChild(anvil); anvil.Render(player, offer);
            anvil.PreviewRequested = (drag, index, revision) => PreviewRequested?.Invoke(drag, index, revision)
                ?? Result.Fail(new Failure(new StringName("ui.gem_unavailable"), "镶嵌暂不可用。"));
            anvil.DropRequested += (drag, index, revision) => DropRequested?.Invoke(drag, index, revision);
        }
        Layout();
    }
}
