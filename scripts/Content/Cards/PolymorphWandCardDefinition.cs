using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 变羊魔棒莫娜卡牌内容定义（内容层）。
public sealed class PolymorphWandCardDefinition : CardDefinition
{
    public PolymorphWandCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(new StringName("card.polymorph_wand"), "变羊魔棒",
                    new StringName("mona"), CardSize.Medium, [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/polymorph_wand-illustration.png"),
                    descriptionEntries:
                    [new(CardKeywords.Activate, "冷却8/7/6秒，魔法消耗0，转变：使随机一张存活的敌方战场小型卡牌转变为同等级的“咩咩羊”；仅本场战斗生效，清除原能力、属性加成和状态，重新开始完整冷却。")]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 80 })),
            new TagSet([GameTags.Equipment]), initialLevel: 2,
            levels: [CreateLevel(2, 80), CreateLevel(3, 70), CreateLevel(4, 60)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int cooldown) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = cooldown },
        [new AbilityDefinition(new StringName("ability.transform_enemy_card"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, cooldown,
            [new TransformRandomEnemyCardEffectDefinition(new BaaSheepCardDefinition())])]);
}
