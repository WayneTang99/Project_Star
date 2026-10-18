using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 护腿帕拉帝恩装备卡牌定义（内容层）。
public sealed class GreavesCardDefinition : CardDefinition
{
    public GreavesCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.greaves"), "护腿", new StringName("paladin"), CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/greaves-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却4秒，魔法消耗0，使左右直接相邻的己方存活战场装备卡牌获得1秒疾速。"),
                new(CardKeywords.Echo, "当直接相邻的己方战场卡牌发动后，己方英雄获得20/40/80/160护甲；多重发动分别触发。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 40 })),
            new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 20), CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int armor) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Armor] = armor },
        [
            new AbilityDefinition(new StringName("ability.hasten_adjacent_equipment"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 40,
                [new ApplyStatusToAdjacentAlliedCardsEffectDefinition(BattleStatus.HasteDuration, 10,
                    GameTags.Equipment, BonusMultiplier: 1, RequiredTag: GameTags.Equipment)]),
            Echo(AdjacentCardSide.Left, new StringName("ability.gain_armor_on_left_activation")),
            Echo(AdjacentCardSide.Right, new StringName("ability.gain_armor_on_right_activation")),
        ]);

    private static AbilityDefinition Echo(AdjacentCardSide side, StringName key) => new(key,
        AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.AlliedHero, 0, 0,
        [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)], triggerCardSide: side);
}
