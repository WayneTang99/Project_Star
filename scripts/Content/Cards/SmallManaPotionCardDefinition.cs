using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 小型魔法药水莫娜卡牌内容定义（内容层）。
public sealed class SmallManaPotionCardDefinition : CardDefinition
{
    public SmallManaPotionCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.small_mana_potion"), "小型魔法药水", new StringName("mona"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/small_mana_potion-illustration-v2.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却3秒，魔法消耗0，恢复己方英雄40/80/160/240魔法。"),
                    new(CardKeywords.Consume, "使用后摧毁此卡牌，仅本场战斗生效。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 30,
            })), new TagSet([GameTags.Consumable]), initialLevel: 1,
            levels: [CreateLevel(1, 40), CreateLevel(2, 80), CreateLevel(3, 160), CreateLevel(4, 240)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.consume_mana_potion"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 30,
            [new RestoreManaEffectDefinition(amount), new DestroyCardEffectDefinition(false)])]);
}
