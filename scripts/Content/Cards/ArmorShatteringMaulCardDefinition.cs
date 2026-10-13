using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 碎甲重锤圣骑士装备：将己方英雄当前护甲消耗为伤害（内容层）。
public sealed class ArmorShatteringMaulCardDefinition : CardDefinition
{
    public ArmorShatteringMaulCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.armor_shattering_maul"), "碎甲重锤", new StringName("paladin"), CardSize.Large, [GameElements.Light],
            illustration: new StringName("res://art/ui/card-face/artwork/armor_shattering_maul-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却6秒，魔法消耗0，己方英雄失去当前全部护甲，对敌方英雄造成等同于实际失去护甲值×5的伤害。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 60 })),
            new TagSet([GameTags.Equipment]), initialLevel: 4,
            levels: [new CardLevelDefinition(4, null,
                [new AbilityDefinition(new StringName("ability.consume_source_hero_armor_damage"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, 60, [new SourceHeroArmorDamageEffectDefinition(Multiplier: 5, ConsumeArmor: true)])])]) { }
}
