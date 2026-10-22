using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 引雷手杖莫娜装备定义（内容层）。
public sealed class LightningConductorStaffCardDefinition : CardDefinition
{
    public LightningConductorStaffCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(
        new CardIdentityAttributes(new StringName("card.lightning_conductor_staff"), "引雷手杖", new StringName("mona"),
            CardSize.Medium, [GameElements.Lightning],
            illustration: new StringName("res://art/ui/card-face/artwork/lightning_conductor_staff-illustration-20261022.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "消耗40魔法，对敌方英雄造成60/120/240伤害。"),
             new(CardKeywords.Aura, "敌方战场有装备卡牌时，此卡牌伤害翻倍。")]),
        baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 60 })),
        new TagSet([GameTags.Equipment]), initialLevel: 2,
        levels: [CreateLevel(2, 60), CreateLevel(3, 120), CreateLevel(4, 240)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 40, 60, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
         new AbilityDefinition(new StringName("ability.enemy_equipment_attack_multiplier"), AbilityActivation.PassiveAura,
            AbilityTarget.SelfCard, 0, 0,
            [new MultiplySourceAttributeWhileEnemyHasTagEffectDefinition(GameAttributeKeys.AttackDamage, GameTags.Equipment)])]);
}
