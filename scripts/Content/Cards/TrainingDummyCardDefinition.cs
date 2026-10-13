using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 训练木桩无阵营材料：周期提升己方全部攻击卡牌的本场攻击。
public sealed class TrainingDummyCardDefinition : CardDefinition
{
    public TrainingDummyCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.training_dummy"), "训练木桩", GameFactions.Neutral, CardSize.Small, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/training_dummy-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却3秒，魔法消耗0，己方所有存活战场攻击卡牌攻击增加5/10/15/20，仅本场战斗生效。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 30 })),
            new TagSet([GameTags.Material]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.increase_allied_attack_cards_attribute"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 30,
            [new ModifyAlliedAttackCardsAttributeEffectDefinition(GameAttributeKeys.AttackDamage, level * 5)])]);
}
