using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 连枷骑士帕拉帝恩卡牌内容定义（内容层）。
public sealed class FlailKnightCardDefinition : CardDefinition
{
    public FlailKnightCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.flail_knight"), "连枷骑士", new StringName("paladin"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/flail_knight-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Activate, "冷却7秒，魔法消耗0，对敌方英雄造成40/80/160伤害；多重1，额外发动一次。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.AttackDamage] = 40,
                [GameAttributeKeys.CooldownTicks] = 70,
                [GameAttributeKeys.Multicast] = 1,
            })), new TagSet([GameTags.Human]), initialLevel: 2,
            levels: [CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 70, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])]);
}
