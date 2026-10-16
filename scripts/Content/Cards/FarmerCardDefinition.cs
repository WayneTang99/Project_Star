using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 农夫帕拉帝恩人类卡牌定义（内容层）。
public sealed class FarmerCardDefinition : CardDefinition
{
    public FarmerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.farmer"), "农夫", new StringName("paladin"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/farmer-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Activate, "冷却9/8/7/6秒，魔法消耗0，己方随机1张其他人类卡牌获得1秒充能。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 90 })),
            new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1, 90), CreateLevel(2, 80), CreateLevel(3, 70), CreateLevel(4, 60)]) { }

    private static CardLevelDefinition CreateLevel(int level, int cooldown) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = cooldown },
        [new AbilityDefinition(new StringName("ability.random_other_allied_cards_charge"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, cooldown, [new ChargeRandomOtherAlliedCardsEffectDefinition(10, 1, GameTags.Human)])]);
}
