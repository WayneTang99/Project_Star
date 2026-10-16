using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 守护十字圣骑士装备卡牌定义（内容层）。
public sealed class GuardianCrossCardDefinition : CardDefinition
{
    public GuardianCrossCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.guardian_cross"), "守护十字", new StringName("paladin"), CardSize.Small, [GameElements.Light],
            illustration: new StringName("res://art/ui/card-face/artwork/guardian_cross-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Echo, "当直接相邻的己方战场卡牌发动后，己方英雄获得10/20/40/80护甲；多重发动分别触发。")])),
            new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int armor) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Armor] = armor },
        [
            Echo(AdjacentCardSide.Left, new StringName("ability.gain_armor_on_left_activation")),
            Echo(AdjacentCardSide.Right, new StringName("ability.gain_armor_on_right_activation")),
        ]);

    private static AbilityDefinition Echo(AdjacentCardSide side, StringName key) => new(key,
        AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.AlliedHero, 0, 0,
        [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)], triggerCardSide: side);
}
