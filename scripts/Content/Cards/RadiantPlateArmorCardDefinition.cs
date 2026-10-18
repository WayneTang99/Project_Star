using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 光辉板甲帕拉帝恩卡牌内容定义（内容层）。
public sealed class RadiantPlateArmorCardDefinition : CardDefinition
{
    public RadiantPlateArmorCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.radiant_plate_armor"), "光辉板甲", new StringName("paladin"),
                CardSize.Medium, [GameElements.Light],
                setKey: new StringName("set.radiant"), illustration: new StringName("res://art/ui/card-face/artwork/radiant_plate_armor-illustration-redraw-20261017-v2.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗0，己方英雄获得160/320护甲。"),
                    new(CardKeywords.Echo, "己方光属性卡牌每次发动后，此卡牌护甲增加20/40，可累加，仅本场战斗生效；包括自身和多重发动。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet([GameTags.Equipment]), initialLevel: 3,
            levels: [CreateLevel(3, 160, 20), CreateLevel(4, 320, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int armor, int growth) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Armor] = armor },
        [
            new AbilityDefinition(new StringName("ability.attribute_armor"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 50,
                [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)]),
            new AbilityDefinition(new StringName("ability.increase_source_armor_on_allied_light_activation"),
                AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.Armor, growth)],
                triggerCardElement: GameElements.Light),
        ]);
}
