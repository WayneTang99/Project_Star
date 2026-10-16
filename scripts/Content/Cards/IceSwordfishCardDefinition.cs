using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 冰剑鱼无阵营水生装备卡牌定义（内容层）。
public sealed class IceSwordfishCardDefinition : CardDefinition
{
    public IceSwordfishCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.ice_swordfish"), "冰剑鱼", new StringName("neutral"),
                CardSize.Medium, [GameElements.Ice],
                illustration: new StringName("res://art/ui/card-face/artwork/ice_swordfish-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却6秒，魔法消耗0，对敌方英雄造成40/80/160伤害。"),
                    new(CardKeywords.Echo, "己方冰属性卡牌发动时，己方随机1张攻击卡牌攻击增加5/10/20，仅本场战斗生效。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 40, [GameAttributeKeys.CooldownTicks] = 60 })),
            new TagSet([GameTags.Equipment, GameTags.Aquatic]), initialLevel: 2,
            levels: [CreateLevel(2, 40, 5), CreateLevel(3, 80, 10), CreateLevel(4, 160, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int bonus) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition("ability.attribute_attack", AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 60, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition("ability.increase_random_allied_attack_attribute_on_element_activation",
                AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new ModifyRandomAlliedAttackCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, bonus)],
                triggerCardElement: GameElements.Ice),
        ]);
}
