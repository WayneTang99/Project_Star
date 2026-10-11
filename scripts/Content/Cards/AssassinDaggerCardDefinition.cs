using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 刺客匕首瓦洛斯卡牌内容定义（内容层）。
public sealed class AssassinDaggerCardDefinition : CardDefinition
{
    public AssassinDaggerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.assassin_dagger"), "刺客匕首", new StringName("valos"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/assassin_dagger-illustration.png"), descriptionEntries:
                [new(CardKeywords.Activate, "冷却4秒，魔法消耗0，对敌方英雄造成10/20/40/80伤害（随此卡牌攻击加成提高）；此卡牌每场战斗首次发动造成的伤害翻倍。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.AttackDamage] = 10,
                [GameAttributeKeys.CooldownTicks] = 40,
            })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.first_activation_attribute_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 40,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage, FirstActivationMultiplier: 2)])]);
}
