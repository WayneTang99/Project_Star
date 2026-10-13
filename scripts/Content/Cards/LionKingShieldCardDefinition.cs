using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 狮王盾圣骑士装备：护甲与卡牌护甲百分比伤害。
public sealed class LionKingShieldCardDefinition : CardDefinition
{
    public LionKingShieldCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.lion_king_shield"), "狮王盾", new StringName("paladin"), CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/lion_king_shield-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却5秒，魔法消耗0，己方英雄获得200/400护甲，对敌方英雄造成等同于此卡牌当前护甲属性50%/80%的伤害。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.Armor] = 200, [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet([GameTags.Equipment]), initialLevel: 3,
            levels: [CreateLevel(3, 200, 50), CreateLevel(4, 400, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int armor, int percent) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Armor] = armor },
        [new AbilityDefinition(new StringName("ability.armor_and_attribute_percent_damage"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 50,
            [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor),
                new AttributeDamageEffectDefinition(GameAttributeKeys.Armor, Percent: percent)])]);
}
