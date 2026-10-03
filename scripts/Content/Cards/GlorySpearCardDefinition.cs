using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 荣耀战矛帕拉帝恩卡牌内容定义（内容层）。
public sealed class GlorySpearCardDefinition : CardDefinition
{
    public GlorySpearCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.glory_spear"), "荣耀战矛", new StringName("paladin"),
                CardSize.Medium, [GameElements.Light],
                illustration: new StringName("res://art/ui/card-face/artwork/glory_spear-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却9秒，魔法消耗0，对敌方英雄造成40伤害（随此卡牌攻击加成提高）。"),
                    new(CardKeywords.Passive, "每当此卡牌位于战场区参与战斗并获胜，此卡牌永久增加20/40/80/160攻击。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.AttackDamage] = 40,
                [GameAttributeKeys.CooldownTicks] = 90,
            })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 20), CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int victoryAttackBonus) => new(level, null,
        [new AbilityDefinition(new StringName("ability.glory_spear_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 90,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])],
        battleVictoryBonuses: [new BattleVictoryAttributeBonus(GameAttributeKeys.AttackDamage, victoryAttackBonus)]);
}
