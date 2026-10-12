using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 弗兰肯特哈尔拉卡牌内容定义（内容层）。
public sealed class FrankentCardDefinition : CardDefinition
{
    public FrankentCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.frankent"), "弗兰肯特", new StringName("harla"),
                CardSize.Large, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/frankent-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却6秒，魔法消耗0，对敌方英雄造成50/100伤害，并施加2/4灼伤和2/4中毒；多重2，额外发动两次。"),
                    new(CardKeywords.Trade, "在战场区或备战区时，每出售一张材料卡牌，此卡牌伤害永久增加10/20，施加的灼伤和中毒永久各增加2/4。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.CooldownTicks] = 60,
                    [GameAttributeKeys.Multicast] = 2,
                })), new TagSet([GameTags.Mechanical]), initialLevel: 3,
            levels: [CreateLevel(3, 50, 2, 10), CreateLevel(4, 100, 4, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int status, int damageBonus) => new(level,
        new Dictionary<StringName, int>
        {
            [GameAttributeKeys.AttackDamage] = damage,
            [GameAttributeKeys.Burn] = status,
            [GameAttributeKeys.Poison] = status,
        },
        [new AbilityDefinition(new StringName("ability.attribute_attack_burn_and_poison"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 60,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
             new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn),
             new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Poison)])],
        saleAttributeBonuses:
        [new TaggedCardSaleAttributeBonus(GameTags.Material, GameAttributeKeys.AttackDamage, damageBonus),
         new TaggedCardSaleAttributeBonus(GameTags.Material, GameAttributeKeys.Burn, status),
         new TaggedCardSaleAttributeBonus(GameTags.Material, GameAttributeKeys.Poison, status)]);
}
