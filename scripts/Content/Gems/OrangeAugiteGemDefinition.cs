using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Gems;

// 发动追加灼伤的正式宝石内容。
public sealed class OrangeAugiteGemDefinition : GemDefinition
{
    public OrangeAugiteGemDefinition() : base(new EntityAttributes<GemIdentityAttributes>(
        new GemIdentityAttributes(new StringName("gem.orange_augite"), "橙辉石",
            "镶嵌在发动卡牌上，发动时额外对敌方施加主属性等值换算后数值10%的灼伤。主属性取第一个发动效果的数值；1点生命再生、魔法再生、灼伤或中毒按10点攻击、护甲或治疗换算。")),
        [new ApplyPrimaryEffectPercentStatusEffectDefinition(BattleStatus.Burn, 10)], new Color("ffae4d")) { }
}
