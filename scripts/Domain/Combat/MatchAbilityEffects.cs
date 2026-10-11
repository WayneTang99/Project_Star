using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 对局事件授予经验，应用层结算（领域能力定义）。
public sealed record GrantExperienceEffectDefinition(int Amount) : EffectDefinition;

// 对局胜利后按来源冻结等级获得指定模板卡牌（领域能力定义）。
public sealed record GrantSourceLevelCardEffectDefinition(CardDefinition Card) : EffectDefinition;

// 根据英雄当前等级提供可撤销的属性贡献（领域能力定义）。
public sealed record ModifyHeroLevelScaledAttributeEffectDefinition(StringName AttributeKey, int Multiplier) : EffectDefinition;

// 将对局事件能力与战斗能力区分，复用同一静态定义（领域规则）。
public static class MatchAbilityRules
{
    // 对局事件不加入战斗发动和回响队列。
    public static bool IsMatchTrigger(AbilityActivation activation) => activation is
        AbilityActivation.PassiveOnRoundStart or AbilityActivation.PassiveOnBattleVictory or AbilityActivation.PassiveOnMonsterVictory;
}
