using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 火焰提灯莫娜卡牌定义，组合随机迟缓与灼伤（内容层）。
public sealed class FlameLanternCardDefinition : CardDefinition
{
    public FlameLanternCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.flame_lantern"), "火焰提灯", new StringName("mona"), CardSize.Small, [GameElements.Fire],
            illustration: new StringName("res://art/ui/card-face/artwork/flame_lantern-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "给予敌方随机1/2/3/4张卡牌1秒迟缓，对敌方英雄施加2灼伤。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 40, [GameAttributeKeys.Burn] = 2 })), new TagSet(), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.random_slow_and_attribute_burn"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 40,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10, level),
                new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)])]);
}
