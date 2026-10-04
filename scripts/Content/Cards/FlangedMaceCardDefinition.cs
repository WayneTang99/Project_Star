using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 钉头页锤帕拉帝恩卡牌内容定义（内容层）。
public sealed class FlangedMaceCardDefinition : CardDefinition
{
    public FlangedMaceCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.flanged_mace"), "钉头页锤", new StringName("paladin"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/flanged_mace-illustration.png"), descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗0，对敌方英雄造成10/20/40/80伤害（随此卡牌攻击加成提高）。"),
                    new(CardKeywords.Aura, "敌方英雄有护甲时，此卡牌伤害翻倍。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 10,
                    [GameAttributeKeys.CooldownTicks] = 50,
                })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 50,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.enemy_armor_attack_multiplier"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new MultiplySourceAttributeWhileEnemyHasArmorEffectDefinition(GameAttributeKeys.AttackDamage)]),
        ]);
}
