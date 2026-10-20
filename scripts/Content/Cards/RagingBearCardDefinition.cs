using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 暴怒熊莫娜卡牌定义，狂暴时倍增伤害与灼伤属性（内容层）。
public sealed class RagingBearCardDefinition : CardDefinition
{
    public RagingBearCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.raging_bear"), "暴怒熊", new StringName("mona"), CardSize.Large, [GameElements.Fire],
            illustration: new StringName("res://art/ui/card-face/artwork/raging_bear-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "对敌方英雄造成80/160/320伤害，施加8/16/32灼伤。"),
                new(CardKeywords.Aura, "狂暴时，此卡牌伤害和灼伤翻倍。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 60 })), new TagSet([GameTags.Beast]), initialLevel: 2,
            levels: [CreateLevel(2, 80, 8), CreateLevel(3, 160, 16), CreateLevel(4, 320, 32)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int burn) => new(level,
        new Dictionary<StringName, int>
        { [GameAttributeKeys.AttackDamage] = damage, [GameAttributeKeys.Burn] = burn },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack_and_burn"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 60,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                    new ApplySourceAttributePercentStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn, 100)]),
            new AbilityDefinition(new StringName("ability.multiply_source_attributes_while_berserk"),
                AbilityActivation.PassiveAura, AbilityTarget.SelfCard, 0, 0,
                [new MultiplySourceAttributeWhileInStateEffectDefinition(GameAttributeKeys.AttackDamage, GameAttributeKeys.Berserk, 2),
                    new MultiplySourceAttributeWhileInStateEffectDefinition(GameAttributeKeys.Burn, GameAttributeKeys.Berserk, 2),
                    new MultiplySourceAttributeWhileInStateEffectDefinition(GameAttributeKeys.BurnBonus, GameAttributeKeys.Berserk, 2)]),
        ]);
}
