using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 弓箭塔帕拉帝恩地域卡牌定义（内容层）。
public sealed class ArcherTowerCardDefinition : CardDefinition
{
    public ArcherTowerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.archer_tower"), "弓箭塔", new StringName("paladin"),
            CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/archer_tower-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却8秒，魔法消耗0，对敌方英雄造成20/40/80/160伤害。"),
                new(CardKeywords.Echo, "当敌方卡牌发动后，此卡牌获得1秒充能。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 80 })), new TagSet([GameTags.Building]),
            initialLevel: 1, levels: [CreateLevel(1, 20), CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 80,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.charge_self_on_enemy_activation"),
                AbilityActivation.EchoOnEnemyCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new ChargeSourceCardEffectDefinition(10)]),
        ]);
}
