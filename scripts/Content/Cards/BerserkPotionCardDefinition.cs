using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 狂暴药水莫娜卡牌内容定义（内容层）。
public sealed class BerserkPotionCardDefinition : CardDefinition
{
    public BerserkPotionCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.berserk_potion"), "狂暴药水", new StringName("mona"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/berserk_potion-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却6秒，魔法消耗0，己方随机一张其他卡牌进入狂暴。"),
                    new(CardKeywords.Consume, "使用后摧毁此卡牌，仅本场战斗生效。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 60,
            })), new TagSet([GameTags.Consumable]), initialLevel: 2,
            levels: [CreateLevel(2), CreateLevel(3), CreateLevel(4)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.consume_berserk_potion"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 60,
            [new SetRandomOtherAlliedCardStateEffectDefinition(GameAttributeKeys.Berserk, true),
                new DestroyCardEffectDefinition(false)])]);
}
