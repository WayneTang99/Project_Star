using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 盾甲士帕拉帝恩卡牌内容定义（内容层）。
public sealed class ShieldBearerCardDefinition : CardDefinition
{
    public ShieldBearerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.shield_bearer"), "盾甲士", new StringName("paladin"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/shield_bearer-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗0，己方英雄获得40护甲（随此卡牌护甲加成提高）。"),
                    new(CardKeywords.Aura, "己方战场区每有一张未被摧毁的人类卡牌，此卡牌护甲增加10/20/40/80，包含自身。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.Armor] = 40,
                [GameAttributeKeys.CooldownTicks] = 50,
            })), new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int armorPerHuman) => new(level, null,
        [
            new AbilityDefinition(new StringName("ability.shield_bearer_armor"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 50,
                [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)]),
            new AbilityDefinition(new StringName("ability.allied_human_armor_aura"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new IncreaseSourceAttributePerAlliedTaggedCardEffectDefinition(
                    GameAttributeKeys.Armor, GameTags.Human, armorPerHuman)]),
        ]);
}
