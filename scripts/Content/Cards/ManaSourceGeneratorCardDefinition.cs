using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔源发电机哈尔拉卡牌内容定义（内容层）。
public sealed class ManaSourceGeneratorCardDefinition : CardDefinition
{
    public ManaSourceGeneratorCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.mana_source_generator"), "魔源发电机", new StringName("harla"),
                CardSize.Medium, [GameElements.Lightning],
                illustration: new StringName("res://art/ui/card-face/artwork/mana_source_generator-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Activate, "冷却5秒，消耗80魔法，己方随机1/2/3张其他卡牌获得1秒充能。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet([GameTags.Mechanical]), initialLevel: 2,
            levels: [CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.random_other_allied_cards_charge"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 80, 50, [new ChargeRandomOtherAlliedCardsEffectDefinition(10, level - 1)])]);
}
