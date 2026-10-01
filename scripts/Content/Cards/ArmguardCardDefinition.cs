using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 臂铠帕拉帝恩卡牌内容定义（内容层）。
public sealed class ArmguardCardDefinition : CardDefinition
{
    public ArmguardCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.armguard"),
                    "臂铠",
                    new StringName("paladin"),
                    CardSize.Small,
                    [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/armguard-illustration.png"),
                    description: "光环：此卡牌具有1层多重（每层多重额外发动一次）；发动：冷却5秒，每次发动对敌方英雄造成5/10/15/20伤害，并使己方英雄获得5/10/15/20护甲。"),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 5,
                    [GameAttributeKeys.Armor] = 5,
                    [GameAttributeKeys.CooldownTicks] = 50,
                    [GameAttributeKeys.Multicast] = 1,
                })),
            new TagSet([GameTags.Equipment]),
            initialLevel: 1,
            levels:
            [
                CreateLevel(1, 5),
                CreateLevel(2, 10),
                CreateLevel(3, 15),
                CreateLevel(4, 20),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int amount) =>
        new(
            level,
            new Dictionary<StringName, int>
            {
                [GameAttributeKeys.AttackDamage] = amount,
                [GameAttributeKeys.Armor] = amount,
            },
            [
                new AbilityDefinition(
                    new StringName("ability.armguard"),
                    AbilityActivation.Active,
                    AbilityTarget.EnemyHero,
                    0,
                    50,
                    [
                        new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                        new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor),
                    ]),
            ]);
}
