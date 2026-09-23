using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 至圣斩刃圣骑士卡牌内容定义（内容层）。
public sealed class HolySlashingBladeCardDefinition : CardDefinition
{
    public HolySlashingBladeCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.holy_slashing_blade"),
                    "至圣斩刃",
                    new StringName("paladin"),
                    CardSize.Large,
                    [GameElements.Light]),
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
                                    [CardSize.Small, CardSize.Medium]),
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
