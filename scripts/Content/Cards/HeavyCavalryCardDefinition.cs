using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 重骑兵圣骑士人类：属性攻防与直接相邻装备的自身光环。
public sealed class HeavyCavalryCardDefinition : CardDefinition
{
    public HeavyCavalryCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.heavy_cavalry"), "重骑兵", new StringName("paladin"), CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/heavy_cavalry-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却8秒，魔法消耗0，对敌方英雄造成80/160伤害，己方英雄获得160/320护甲。"),
                new(CardKeywords.Aura, "此卡牌每与己方1张存活战场装备卡牌直接相邻，攻击和护甲各增加60/120。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 80, [GameAttributeKeys.Armor] = 160, [GameAttributeKeys.CooldownTicks] = 80 })),
            new TagSet([GameTags.Human]), initialLevel: 3,
            levels: [CreateLevel(3, 80, 160, 60), CreateLevel(4, 160, 320, 120)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int armor, int bonus) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage, [GameAttributeKeys.Armor] = armor },
        [
            new AbilityDefinition(new StringName("ability.attack_and_gain_source_hero_armor"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 80,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage), new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)]),
            new AbilityDefinition(new StringName("ability.adjacent_equipment_attack_armor_aura"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new IncreaseSourceAttributePerAlliedTaggedCardEffectDefinition(GameAttributeKeys.AttackDamage, GameTags.Equipment, bonus, AdjacentOnly: true),
                    new IncreaseSourceAttributePerAlliedTaggedCardEffectDefinition(GameAttributeKeys.Armor, GameTags.Equipment, bonus, AdjacentOnly: true)]),
        ]);
}
