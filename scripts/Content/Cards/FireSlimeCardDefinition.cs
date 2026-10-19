using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 火史莱姆无阵营卡牌定义，复用通用属性状态效果（内容层）。
public sealed class FireSlimeCardDefinition : CardDefinition
{
    public FireSlimeCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.fire_slime"), "火史莱姆", GameFactions.Neutral, CardSize.Small, [GameElements.Fire],
            illustration: new StringName("res://art/ui/card-face/artwork/fire_slime-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却3秒，魔法消耗0，对敌方英雄施加2/4/8/16灼伤。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 30 })),
            new TagSet([GameTags.Material]), initialLevel: 1,
            levels: [CreateLevel(1, 2), CreateLevel(2, 4), CreateLevel(3, 8), CreateLevel(4, 16)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Burn] = amount },
        [new AbilityDefinition(new StringName("ability.attribute_burn"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 30,
            [new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)])]);
}
