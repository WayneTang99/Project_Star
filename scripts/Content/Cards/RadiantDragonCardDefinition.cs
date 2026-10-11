using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 辉耀巨龙帕拉帝恩卡牌内容定义（内容层）。
public sealed class RadiantDragonCardDefinition : CardDefinition
{
    public RadiantDragonCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.radiant_dragon"), "辉耀巨龙", new StringName("paladin"),
                CardSize.Large, [GameElements.Fire, GameElements.Light],
                illustration: new StringName("res://art/ui/card-face/artwork/radiant_dragon-illustration-rework-20261011-goldwhite.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却8秒，魔法消耗0，对敌方英雄造成100/200伤害，并施加等同于此卡牌当前伤害10%的灼伤（向下取整）。"),
                    new(CardKeywords.Aura, "此卡牌飞行时，冷却缩短4秒。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 100,
                    [GameAttributeKeys.CooldownTicks] = 80,
                })), new TagSet([GameTags.Mount, GameTags.Dragon]), initialLevel: 3,
            levels: [CreateLevel(3, 100), CreateLevel(4, 200)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack_and_percent_burn"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 80,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                    new ApplySourceAttributePercentStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.AttackDamage, 10)]),
            new AbilityDefinition(new StringName("ability.flying_cooldown_aura"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new ReduceSourceCooldownWhileInStateEffectDefinition(GameAttributeKeys.Flying, 40)]),
        ]);
}
