using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 圣殿骑士帕拉帝恩卡牌内容定义（内容层）。
public sealed class TemplarKnightCardDefinition : CardDefinition
{
    public TemplarKnightCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.templar_knight"), "圣殿骑士", new StringName("paladin"),
                CardSize.Medium, [GameElements.Light],
                illustration: new StringName("res://art/ui/card-face/artwork/templar_knight-illustration.png"), descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却4秒，魔法消耗0，对敌方英雄造成60/120伤害（随此卡牌攻击加成提高）。"),
                    new(CardKeywords.Summon, "战斗开始时，分别在左右紧邻的空格召唤一张同级骑佩短剑；该侧无空间或已有卡牌时不召唤，召唤物仅本场战斗存在。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 60,
                    [GameAttributeKeys.CooldownTicks] = 40,
                })), new TagSet([GameTags.Human]), initialLevel: 3,
            levels: [CreateLevel(3, 60), CreateLevel(4, 120)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 40, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.summon_adjacent_cards"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0,
                [new SummonAdjacentCardEffectDefinition(new RidingShortswordCardDefinition(), AdjacentCardSide.Left),
                    new SummonAdjacentCardEffectDefinition(new RidingShortswordCardDefinition(), AdjacentCardSide.Right)]),
        ]);
}
