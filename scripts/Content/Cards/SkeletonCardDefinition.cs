using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 骷髅莫娜卡牌内容定义（内容层）。
public sealed class SkeletonCardDefinition : CardDefinition
{
    public SkeletonCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.skeleton"), "骷髅", new StringName("mona"),
                CardSize.Small, [GameElements.Dark],
                illustration: new StringName("res://art/ui/card-face/artwork/skeleton-floating-skull-20261020.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却3秒，魔法消耗0，对敌方英雄造成10/20/30/40伤害（随此卡牌攻击加成提高）。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 10,
                    [GameAttributeKeys.CooldownTicks] = 30,
                })), new TagSet([GameTags.Undead]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = level * 10 },
        [new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 30, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])]);
}
