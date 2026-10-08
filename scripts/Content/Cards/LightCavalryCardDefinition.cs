using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 轻骑兵帕拉帝恩卡牌内容定义（内容层）。
public sealed class LightCavalryCardDefinition : CardDefinition
{
    public LightCavalryCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.light_cavalry"),
                    "轻骑兵",
                    new StringName("paladin"),
                    CardSize.Medium,
                    [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/light_cavalry-illustration-v2.png"),
                    descriptionEntries:
                    [
                        new(CardKeywords.Activate, "冷却3秒，对敌方英雄造成10/20/40/60伤害，并使己方战场区具备攻击能力的人类卡牌攻击增加10/20/40/60（包括此卡牌，可累计）；首次发动时，此卡牌冷却增加2秒。"),
                    ]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 0,
                    [GameAttributeKeys.CooldownTicks] = 30,
                })),
            new TagSet([GameTags.Human]),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 10),
                CreateLevel(2, 20),
                CreateLevel(3, 40),
                CreateLevel(4, 60),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int attackValue) =>
        new(
            level,
            new Dictionary<StringName, int>
            {
                [GameAttributeKeys.AttackDamage] = attackValue,
            },
            [
                new AbilityDefinition(
                    new StringName("ability.light_cavalry_support"),
                    AbilityActivation.Active,
                    AbilityTarget.EnemyHero,
                    0,
                    30,
                    [
                        new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                        new IncreaseSourceCooldownEffectDefinition(20, FirstActivationOnly: true),
                        new ModifyTaggedAlliedCardsAttributeEffectDefinition(
                            GameTags.Human,
                            GameAttributeKeys.AttackDamage,
                            attackValue),
                    ]),
            ]);
}
