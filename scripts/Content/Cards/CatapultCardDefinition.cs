using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 投石车帕拉帝恩机械卡牌定义（内容层）。
public sealed class CatapultCardDefinition : CardDefinition
{
    public CatapultCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.catapult"), "投石车", new StringName("paladin"),
                CardSize.Large, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/catapult-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却12秒，魔法消耗0，对敌方英雄造成80/160/320伤害（随此卡牌攻击加成提高）。"),
                    new(CardKeywords.Aura, "己方战场每有一张未被摧毁的人类卡牌，此卡牌冷却缩短1秒，完整冷却最低1秒。"),
                    new(CardKeywords.Aura, "此卡牌获得等同于有效完整冷却秒数×20/30/40的攻击力（向下取整），随冷却变化动态调整。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 80, [GameAttributeKeys.CooldownTicks] = 120 })),
            new TagSet([GameTags.Mechanical]), initialLevel: 2,
            levels: [CreateLevel(2, 80, 20), CreateLevel(3, 160, 30), CreateLevel(4, 320, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int perSecond) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition("ability.attribute_attack", AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 120, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition("ability.allied_tag_count_cooldown_aura", AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0, [new ReduceSourceCooldownPerAlliedTaggedCardEffectDefinition(GameTags.Human, 10)]),
            new AbilityDefinition("ability.cooldown_attribute_aura", AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new IncreaseSourceAttributePerCooldownSecondEffectDefinition(GameAttributeKeys.AttackDamage, perSecond)]),
        ]);
}
