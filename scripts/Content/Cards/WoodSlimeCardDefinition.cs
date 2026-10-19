using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 木史莱姆无阵营卡牌定义，复用通用属性状态效果（内容层）。
public sealed class WoodSlimeCardDefinition : CardDefinition
{
    public WoodSlimeCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.wood_slime"), "木史莱姆", GameFactions.Neutral, CardSize.Small, [GameElements.Wood],
            illustration: new StringName("res://art/ui/card-face/artwork/wood_slime-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却3秒，魔法消耗0，对敌方英雄施加2/4/8/16中毒。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 30 })),
            new TagSet([GameTags.Material]), initialLevel: 1,
            levels: [CreateLevel(1, 2), CreateLevel(2, 4), CreateLevel(3, 8), CreateLevel(4, 16)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Poison] = amount },
        [new AbilityDefinition(new StringName("ability.attribute_poison"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 30,
            [new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Poison)])]);
}
