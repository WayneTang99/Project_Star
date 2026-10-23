using Godot;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.CardFace;

// 卡面效果行，紧凑字号按实际布局设置。
public sealed partial class CardEffectRow : HBoxContainer
{
    private TextureRect _icon = null!;
    private Label _value = null!;

    public override void _Ready()
    {
        _icon = GetNode<TextureRect>("Icon");
        _value = GetNode<Label>("Value");
    }

    private StyleBoxTexture? _plate;
    public override void _Draw()
    {
        if (_plate is not null) DrawStyleBox(_plate, new Rect2(new Vector2(-5, 0), Size + new Vector2(12, 0)));
    }

    // 按效果语义选择图标与显示值。
    public void Configure(CardFaceEffect effect)
    {
        var kind = effect.Kind is CardFaceEffectKind.ManaCost or CardFaceEffectKind.ManaRegen ? "mana"
            : effect.Kind == CardFaceEffectKind.HealthRegen ? "healing" : effect.Kind.ToString().ToLowerInvariant();
        // 保持样式及纹理的强引用，避免GC后已有绘制命令引用失效纹理。
        _plate = MatchTheme.Plate("effect-" + kind, 3);
        QueueRedraw();
        _icon.Texture = MatchTheme.Icon(new StringName(effect.Kind is CardFaceEffectKind.ManaRegen or CardFaceEffectKind.HealthRegen ? "regeneration" : kind));
        _value.Text = effect.Value;
        _icon.Modulate = AttributePalette.Effect(effect.Kind);
        _icon.Material = AttributePalette.IconMaterial;
        _value.AddThemeColorOverride("font_outline_color", new Color("241810"));
        MatchTheme.Text(_value, 15, new Color("fff2cd"), true, true);
    }

    // 缩小装饰留白并保留文字下限，长值完整内容由详情承载。
    public void SetDisplayMetrics(int font, float height, int iconSize = 12)
    {
        CustomMinimumSize = new Vector2(0, height);
        _icon.CustomMinimumSize = new Vector2(iconSize, iconSize);
        _icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        AddThemeConstantOverride("separation", 4);
        _value.CustomMinimumSize = Vector2.Zero;
        _value.AddThemeFontSizeOverride("font_size", font);
        _value.ClipText = true; _value.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        QueueRedraw();
    }

    // 两位数使用真实字体宽度，长公式超过可用宽度时才省略。
    public float PreferredWidth(int font, int iconSize = 12) => Mathf.Max(40, 5 + iconSize + 4 + MatchTheme.Font(true, true).GetStringSize(_value.Text, fontSize: font).X + 7);
}
