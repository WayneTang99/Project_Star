using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 教团远征军帕拉帝恩卡牌内容定义（内容层）。
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
                    [GameElements.Light],
                    illustration: new StringName("res://art/ui/card-face/artwork/order_crusader-illustration.png"),
                    descriptionEntries:
                    [
                        new(CardKeywords.Activate, "冷却8秒，对敌方英雄造成40/80/120伤害"),
                        new(CardKeywords.Aura, "敌方小型卡牌在战斗中视为恶魔，敌方每有一件未被摧毁的恶魔卡牌，此卡牌伤害增加10/20/40；此卡牌被摧毁后，赋予的恶魔标签立即失效。"),
                    ]),
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
