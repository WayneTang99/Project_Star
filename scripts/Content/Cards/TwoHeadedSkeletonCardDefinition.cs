using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 双头骷髅莫娜亡灵卡牌，摧毁后原位分裂为两张同级骷髅（内容层）。
public sealed class TwoHeadedSkeletonCardDefinition : CardDefinition
{
    public TwoHeadedSkeletonCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.two_headed_skeleton"), "双头骷髅", new StringName("mona"),
            CardSize.Medium, [GameElements.Dark],
            illustration: new StringName("res://art/ui/card-face/artwork/two_headed_skeleton-illustration-20261022.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "对敌方英雄造成60/120/240伤害。"),
             new(CardKeywords.Deathrattle, "在此卡牌原位置召唤2张同级【骷髅】。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 70, [GameAttributeKeys.Multicast] = 1 })),
            new TagSet([GameTags.Undead]), initialLevel: 2,
            levels: [CreateLevel(2, 60), CreateLevel(3, 120), CreateLevel(4, 240)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 70, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
         new AbilityDefinition(new StringName("ability.summon_on_source_destroyed"), AbilityActivation.EchoOnSourceDestroyed,
            AbilityTarget.SelfCard, 0, 0,
            [new SummonAtDestroyedCardEffectDefinition(new SkeletonCardDefinition(), UseSourceLevel: true, Count: 2)])]);
}
