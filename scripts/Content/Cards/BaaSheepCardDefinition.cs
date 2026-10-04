using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 咩咩羊无阵营卡牌内容定义（内容层）。
public sealed class BaaSheepCardDefinition : CardDefinition
{
    public BaaSheepCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(new StringName("card.baa_sheep"), "咩咩羊",
                    new StringName("neutral"), CardSize.Small, [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/baa_sheep-illustration.png"),
                    descriptionEntries:
                    [new(CardKeywords.Activate, "冷却5秒，魔法消耗0，此卡牌获得1/2/3/4秒疾速。")]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet([GameTags.Beast]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.hasten_self"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 50,
            [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, level * 10)])]);
}
