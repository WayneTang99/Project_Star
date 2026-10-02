using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 炼金釜莫娜卡牌内容定义（内容层）。
public sealed class AlchemyCauldronCardDefinition : CardDefinition
{
    public AlchemyCauldronCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.alchemy_cauldron"), "炼金釜", new StringName("mona"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/placeholder.svg"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却6秒，魔法消耗0，对敌方英雄施加10中毒和10灼伤。"),
                    new(CardKeywords.Passive, "在战场区或备战区时，当你出售一张消耗品卡牌后，此卡牌施加的中毒和灼伤永久各增加6/12/20。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 60,
                [GameAttributeKeys.Poison] = 10,
                [GameAttributeKeys.Burn] = 10,
            })),
            new TagSet(), initialLevel: 2,
            levels: [CreateLevel(2, 6), CreateLevel(3, 12), CreateLevel(4, 20)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int bonus) => new(level, null,
        [new AbilityDefinition(new StringName("ability.apply_poison_and_burn"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 60,
            [new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Poison),
             new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)])],
        saleAttributeBonuses:
        [new TaggedCardSaleAttributeBonus(GameTags.Consumable, GameAttributeKeys.Poison, bonus),
         new TaggedCardSaleAttributeBonus(GameTags.Consumable, GameAttributeKeys.Burn, bonus)]);
}
