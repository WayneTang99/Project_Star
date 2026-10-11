using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 机关重弩哈尔拉卡牌内容定义（内容层）。
public sealed class MechanismHeavyCrossbowCardDefinition : CardDefinition
{
    public MechanismHeavyCrossbowCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.mechanism_heavy_crossbow"), "机关重弩", new StringName("harla"),
                CardSize.Large, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/mechanism_heavy_crossbow-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却12秒，对敌方英雄造成80/160/320伤害；多重2，额外发动两次。"),
                    new(CardKeywords.Aura, "与己方机械卡牌直接相邻时，此卡牌冷却缩短2秒；两侧均有机械卡牌时不重复缩短。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 80,
                    [GameAttributeKeys.CooldownTicks] = 120,
                    [GameAttributeKeys.Multicast] = 2,
                })), new TagSet([GameTags.Mechanical]), initialLevel: 2,
            levels: [CreateLevel(2, 80), CreateLevel(3, 160), CreateLevel(4, 320)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 120, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.adjacent_tag_cooldown_aura"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new ReduceSourceCooldownWhileAdjacentTaggedCardEffectDefinition(GameTags.Mechanical, 20)]),
        ]);
}
