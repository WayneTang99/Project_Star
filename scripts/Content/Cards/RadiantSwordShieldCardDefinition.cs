using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 光辉剑盾只组合通用伤害、护甲与限定标签的元素冷却光环。
public sealed class RadiantSwordShieldCardDefinition : CardDefinition
{
    public RadiantSwordShieldCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.radiant_sword_shield"), "光辉剑盾", new StringName("paladin"), CardSize.Medium,
            [GameElements.Light], setKey: new StringName("set.radiant"),
            illustration: new StringName("res://art/ui/card-face/artwork/radiant_sword_shield-illustration-20261018.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却5秒，魔法消耗0，对敌方英雄造成200伤害，己方英雄获得200护甲；多重1。"),
                new(CardKeywords.Aura, "己方存活战场光属性装备卡牌冷却缩减10%，包含此卡牌；多张百分比累加，最高100%，此卡牌被摧毁后失效。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 50 })), new TagSet([GameTags.Equipment]), initialLevel: 4,
            levels:
            [new CardLevelDefinition(4, new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 200, [GameAttributeKeys.Armor] = 200, [GameAttributeKeys.Multicast] = 1 },
                [new AbilityDefinition(new StringName("ability.attribute_damage_and_armor"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, 50,
                    [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage), new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)]),
                 new AbilityDefinition(new StringName("ability.allied_light_equipment_cooldown_aura"), AbilityActivation.PassiveAura,
                    AbilityTarget.SelfCard, 0, 0, [new ReduceAlliedElementCardCooldownAuraEffectDefinition(GameElements.Light, 10, GameTags.Equipment)])])]) { }
}
