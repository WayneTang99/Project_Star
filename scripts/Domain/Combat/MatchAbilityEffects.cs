using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 轮开始永久缩减能力来源套装内战场卡牌的冷却倍率。
public sealed record ReduceSetCardCooldownEffectDefinition(int Percent) : EffectDefinition;

// 英雄每升一级随机为来源卡牌的第一个空孔镶嵌宝石（领域能力定义）。
public sealed record SocketRandomSourceGemEffectDefinition : EffectDefinition;

// 对局事件授予经验，应用层结算（领域能力定义）。
public sealed record GrantExperienceEffectDefinition(int Amount) : EffectDefinition;

// 对局胜利后按来源冻结等级获得指定模板卡牌（领域能力定义）。
public sealed record GrantSourceLevelCardEffectDefinition(CardDefinition Card) : EffectDefinition;

// 对局触发时按来源等级获得当前英雄以外（含无阵营）的指定尺寸随机卡牌。
public sealed record GrantRandomOtherHeroSourceLevelCardEffectDefinition(CardSize Size, CardSize? AdditionalSize = null) : EffectDefinition;

// 拾取时按尺寸与标签随机获得一张来源同级卡牌，不限制英雄归属（领域对局效果）。
public sealed record GrantRandomTaggedSourceLevelCardEffectDefinition(CardSize Size, StringName RequiredTag) : EffectDefinition;

// 轮初从全部英雄的同级技能候选中随机学习一项（领域对局定义）。
public sealed record GrantRandomHeroSourceLevelSkillEffectDefinition : EffectDefinition;

// 根据英雄当前等级提供可撤销的属性贡献（领域能力定义）。
public sealed record ModifyHeroLevelScaledAttributeEffectDefinition(StringName AttributeKey, int Multiplier) : EffectDefinition;

// 将对局事件能力与战斗能力区分，复用同一静态定义（领域规则）。
public static class MatchAbilityRules
{
    // 对局事件不加入战斗发动和回响队列。
    public static bool IsMatchTrigger(AbilityActivation activation) => activation is
        AbilityActivation.PassiveOnRoundStart or AbilityActivation.PassiveOnBattleVictory or AbilityActivation.PassiveOnMonsterVictory
        or AbilityActivation.PassiveOnPickup or AbilityActivation.PassiveOnHeroLevelUp;
}
