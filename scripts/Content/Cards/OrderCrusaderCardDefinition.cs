using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 教团远征军圣骑士卡牌内容定义（内容层）。
public sealed class OrderCrusaderCardDefinition : CardDefinition
{
    public OrderCrusaderCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.order_crusader"),
                    "教团远征军",
                    new StringName("paladin"),
                    CardSize.Large,
                    [GameElements.Light]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 40,
                    [GameAttributeKeys.CooldownTicks] = 80,
                })),
            new TagSet([GameTags.Human]),
            initialLevel: 2,
            levels:
            [
                CreateLevel(2, 40, 10),
                CreateLevel(3, 80, 20),
                CreateLevel(4, 120, 40),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int damage, int bonusPerDemon) =>
        new(
            level,
            new Dictionary<StringName, int>
            {
                [GameAttributeKeys.AttackDamage] = damage,
            },
            [
                new AbilityDefinition(
                    new StringName("ability.order_crusader_attack"),
                    AbilityActivation.Active,
                    AbilityTarget.EnemyHero,
                    0,
                    80,
                    [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
                new AbilityDefinition(
                    new StringName("ability.order_crusader_demon_aura"),
                    AbilityActivation.PassiveAura,
                    AbilityTarget.SelfCard,
                    0,
                    0,
                    [
                        new GrantTagToEnemySizeCardsEffectDefinition(CardSize.Small, GameTags.Demon),
                        new IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition(
                            GameAttributeKeys.AttackDamage,
                            GameTags.Demon,
                            bonusPerDemon),
                    ]),
            ]);
}
