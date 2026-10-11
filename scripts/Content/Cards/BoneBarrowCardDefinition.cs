using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 骨冢莫娜卡牌内容定义（内容层）。
public sealed class BoneBarrowCardDefinition : CardDefinition
{
    public BoneBarrowCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.bone_barrow"), "骨冢", new StringName("mona"),
                CardSize.Medium, [GameElements.Dark],
                illustration: new StringName("res://art/ui/card-face/artwork/bone_barrow-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却3秒，魔法消耗0，此卡牌左侧紧邻位置可用时，召唤一张同级骷髅；左侧已有存活卡牌或超出战场边界时不召唤，已摧毁卡牌的位置可再次召唤，召唤物仅本场战斗存在。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.CooldownTicks] = 30,
                })), new TagSet([GameTags.Undead, GameTags.Location]), initialLevel: 2,
            levels: [CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.summon_adjacent_cards"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 30,
            [new SummonAdjacentCardEffectDefinition(new SkeletonCardDefinition(), AdjacentCardSide.Left)])]);
}
