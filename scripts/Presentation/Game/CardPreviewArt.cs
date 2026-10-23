using System;
using Godot;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Game;

// 将完整卡面投影到透视平面，拖动仅改变预览角度（表现层）。
public sealed partial class CardPreviewArt : Control
{
    private SubViewport _viewport = null!;
    private TextureRect _image = null!;
    private Project_Star.Presentation.CardFace.CardFace _face = null!;
    private ShaderMaterial _material = null!;
    private Vector2 _start;
    private Vector2 _angle;
    private bool _dragging;
    private Vector2 _cardSize;
    public Vector2 TiltAngle => _angle;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _viewport = new SubViewport { TransparentBg = true, Disable3D = true, HandleInputLocally = false, RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
        AddChild(_viewport);
        _face = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn").Instantiate<Project_Star.Presentation.CardFace.CardFace>(); _face.PreviewMetrics = true; _viewport.AddChild(_face);
        _material = new ShaderMaterial { Shader = new Shader { Code = """
shader_type canvas_item;
uniform vec2 tilt = vec2(0.0);
uniform float aspect = 1.0;
uniform vec2 content_scale = vec2(1.0);
void fragment(){
    vec2 p=(UV-0.5)*vec2(aspect,1.0)*1.35;
    float cx=cos(tilt.x),sx=sin(tilt.x),cy=cos(tilt.y),sy=sin(tilt.y);
    vec3 axis_x=vec3(cy,0.0,-sy);
    vec3 axis_y=vec3(sy*sx,cx,cy*sx);
    vec3 normal=cross(axis_x,axis_y);
    vec3 origin=vec3(0.0,0.0,2.5);
    vec3 ray=vec3(p,-2.5);
    float distance=-dot(normal,origin)/dot(normal,ray);
    vec3 hit=origin+ray*distance;
    vec2 uv=vec2(dot(hit,axis_x)/aspect,dot(hit,axis_y))*content_scale+0.5;
    vec4 sampled=texture(TEXTURE,uv);
    COLOR=sampled*float(uv.x>=0.0&&uv.x<=1.0&&uv.y>=0.0&&uv.y<=1.0);
}
""" } };
        _image = new TextureRect { Texture = _viewport.GetTexture(), Material = _material, MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        AddChild(_image); _image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Resized += LayoutCard;
    }
    // 直接渲染已有卡面ViewModel，特效与规则数据分离。
    public void Render(CardFaceViewModel card) { ResetTilt(); _face.SetCard(card); LayoutCard(); }
    private void LayoutCard()
    {
        if (_face is null || Size.X < 1 || Size.Y < 1) return;
        ResetTilt();
        _cardSize = new Vector2(Size.X / 1.35f, Size.Y / 1.35f);
        var pixelScale = Math.Max(1, GetGlobalTransform().Scale.X * GetViewport().GetStretchTransform().Scale.X);
        var frameSize = _cardSize * pixelScale;
        const int padding = 24;
        _viewport.Size = new Vector2I((int)Math.Ceiling(frameSize.X) + padding * 2, (int)Math.Ceiling(frameSize.Y) + padding * 2);
        _face.SetDisplaySize(_cardSize); _face.Scale = Vector2.One * pixelScale; _face.Position = new Vector2(padding, padding);
        _material.SetShaderParameter("content_scale", frameSize / new Vector2(_viewport.Size.X, _viewport.Size.Y));

        _material.SetShaderParameter("aspect", _cardSize.X / Math.Max(1, _cardSize.Y));
    }
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
        {
            if (button.Pressed && new Rect2((Size - _cardSize) / 2, _cardSize).HasPoint(button.Position)) { _dragging = true; _start = GetLocalMousePosition(); MouseDefaultCursorShape = CursorShape.Drag; AcceptEvent(); }
            else if (!button.Pressed) _dragging = false;
        }
    }
    public override void _Input(InputEvent input)
    {
        if (!IsVisibleInTree()) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false }) _dragging = false;
        if (_dragging && input is InputEventMouseMotion motion)
        {
            var offset = GetGlobalTransform().AffineInverse() * motion.GlobalPosition - _start;
            _angle = new Vector2(Mathf.Clamp(-offset.Y / Math.Max(1, Size.Y) * 48, -18, 18), Mathf.Clamp(offset.X / Math.Max(1, Size.X) * 48, -18, 18));
            GetViewport().SetInputAsHandled();
        }
    }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || !GetWindow().HasFocus()) { ResetTilt(); return; }
        if (!_dragging) { _angle = _angle.Lerp(Vector2.Zero, 1 - Mathf.Exp(-12 * (float)delta)); MouseDefaultCursorShape = CursorShape.PointingHand; }
        _material.SetShaderParameter("tilt", _angle * (Mathf.Pi / 180));
    }
    // 关闭、换卡和缩放时归零，避免旧拖拽状态泄漏。
    public void ResetTilt() { _dragging = false; _angle = Vector2.Zero; _material?.SetShaderParameter("tilt", Vector2.Zero); }
    public override void _ExitTree() => Resized -= LayoutCard;
}
