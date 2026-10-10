using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔法坩埚莫娜卡牌内容定义（内容层）。
public sealed class MagicCauldronCardDefinition : CardDefinition
{
    public MagicCauldronCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.magic_cauldron"), "魔法坩埚", new StringName("mona"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/magic_cauldron-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却7秒，魔法消耗0，对敌方英雄施加3中毒（随此卡牌施毒加成提高）。"),
                    new(CardKeywords.Trade, "在战场区或备战区时，每出售一张植物卡牌，此卡牌施加的中毒永久增加1/2/3/4。"),
                    new(CardKeywords.Quest, "累计出售20张植物卡牌，解锁：此卡牌冷却减少2秒。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 70,
                [GameAttributeKeys.Poison] = 3,
            })),
            new TagSet(), initialLevel: 1,
            levels: [CreateLevel(1, 1), CreateLevel(2, 2), CreateLevel(3, 3), CreateLevel(4, 4)],
            quests:
            [new CardQuestDefinition(new StringName("quest.magic_cauldron.sell_plants"),
                new SoldTaggedCardQuestConditionDefinition(GameTags.Plant), 20,
                [new AbilityDefinition(new StringName("ability.quest_reduce_cooldown"),
                    AbilityActivation.PassiveWhileEnabled, AbilityTarget.SelfCard, 0, 0,
                    [new ModifyAttributeEffectDefinition(GameAttributeKeys.CooldownTicks, -20)])])])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int bonus) => new(level, null,
        [new AbilityDefinition(new StringName("ability.magic_cauldron_poison"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 70,
            [new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Poison)])],
        saleAttributeBonuses: [new TaggedCardSaleAttributeBonus(GameTags.Plant, GameAttributeKeys.Poison, bonus)]);
}
