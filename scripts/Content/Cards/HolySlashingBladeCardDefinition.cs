using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 黎明之剑帕拉帝恩卡牌内容定义（内容层）。
public sealed class HolySlashingBladeCardDefinition : CardDefinition
{
    public HolySlashingBladeCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.holy_slashing_blade"),
                    "黎明之剑",
                    new StringName("paladin"),
                    CardSize.Large,
                    [GameElements.Light],
                    illustration: new StringName("res://art/ui/card-face/artwork/holy_slashing_blade-illustration.png"),
                    description: "发动：冷却10秒，对敌方英雄造成200伤害，并随机本场摧毁敌方一件小型恶魔或亡灵卡牌；光环：本场战斗双方每有一件已被摧毁的恶魔或亡灵卡牌，此卡牌攻击翻倍（本次发动摧毁的卡牌立即计数）。"),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 200,
                    [GameAttributeKeys.CooldownTicks] = 100,
                })),
            new TagSet([GameTags.Equipment]),
            initialLevel: 4,
            levels:
            [
                new CardLevelDefinition(
                    4,
                    null,
                    [
                        new AbilityDefinition(
                            new StringName("ability.holy_slashing_blade"),
                            AbilityActivation.Active,
                            AbilityTarget.EnemyHero,
                            0,
                            100,
                            [
                                new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                                new DestroyRandomEnemyCardEffectDefinition(
                                    [GameTags.Demon, GameTags.Undead],
                                    [CardSize.Small]),
                            ]),
                        new AbilityDefinition(
                            new StringName("ability.destroyed_unholy_attack_multiplier"),
                            AbilityActivation.PassiveAura,
                            AbilityTarget.SelfCard,
                            0,
                            0,
                            [new MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition(
                                GameAttributeKeys.AttackDamage,
                                [GameTags.Demon, GameTags.Undead])]),
                    ]),
            ])
    {
    }
}
