using Godot;
using Project_Star.Domain.Common;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 属性语义色独立于桌面主题、元素及等级框；展示层不改变属性值。
internal static class AttributePalette
{
    // SVG保留轮廓／透明度，以Modulate给出精确语义色而非乘上原烘焙颜色。
    internal static ShaderMaterial IconMaterial { get; } = new()
    {
        Shader = new Shader { Code = "shader_type canvas_item; varying vec4 tint; void vertex(){ tint = COLOR; } void fragment(){ COLOR = vec4(tint.rgb, texture(TEXTURE, UV).a * tint.a); }" }
    };
    internal static readonly StringName Health = new("Health");
    internal static readonly StringName Haste = new("Haste");
    internal static readonly StringName Slow = new("Slow");
    internal static readonly StringName Immobilize = new("Immobilize");

    internal static Color? Find(StringName key) => key == GameAttributeKeys.AttackDamage ? new Color("ff9c83")
        : key == GameAttributeKeys.Armor ? new Color("8ccbff")
        : key == GameAttributeKeys.Poison ? new Color("b5dc65")
        : key == GameAttributeKeys.Burn ? new Color("ffba66")
        : key == GameAttributeKeys.HealingBonus ? new Color("7ad99b")
        : key == Health || key == GameAttributeKeys.MaxHealth ? new Color("f38faa")
        : key == GameAttributeKeys.Mana || key == GameAttributeKeys.MaxMana || key == GameAttributeKeys.ManaRegen ? new Color("b6a1f2")
        : key == GameAttributeKeys.HealthRegen ? new Color("70d6b6")
        : key == GameAttributeKeys.Value || key == GameAttributeKeys.Wealth || key == GameAttributeKeys.Income ? new Color("f4d56b")
        : key == Haste || key == GameAttributeKeys.HasteDurationBonus ? new Color("66d9e8")
        : key == Slow ? new Color("c0aaa0")
        : key == Immobilize ? new Color("e2b8e8") : null;

    internal static Color Effect(CardFaceEffectKind kind) => Find(kind switch
    {
        CardFaceEffectKind.Damage => GameAttributeKeys.AttackDamage,
        CardFaceEffectKind.Armor => GameAttributeKeys.Armor,
        CardFaceEffectKind.Healing => GameAttributeKeys.HealingBonus,
        CardFaceEffectKind.Poison => GameAttributeKeys.Poison,
        CardFaceEffectKind.Burn => GameAttributeKeys.Burn,
        CardFaceEffectKind.Mana => GameAttributeKeys.Mana,
        _ => new StringName("")
    }) ?? MatchTheme.Ink;

    internal static StringName TextKey(string text) => text switch
    {
        "攻击" or "伤害" or "普通伤害" => GameAttributeKeys.AttackDamage,
        "护甲" => GameAttributeKeys.Armor,
        "中毒" or "Poison" => GameAttributeKeys.Poison,
        "灼伤" or "Burn" => GameAttributeKeys.Burn,
        "治疗" or "治疗加成" => GameAttributeKeys.HealingBonus,
        "生命" or "生命值" or "最大生命" or "最大生命值" => Health,
        "魔法" or "魔法回复" => GameAttributeKeys.Mana,
        "再生" => GameAttributeKeys.HealthRegen,
        "价值" or "金币" or "金钱" => GameAttributeKeys.Value,
        "疾速" or "疾速时长加成" or "Haste" => Haste,
        "迟缓" or "Slow" => Slow,
        "禁锢" or "Immobilize" => Immobilize,
        _ => new StringName("")
    };
}
