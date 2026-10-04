using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 风之刃极云卡牌内容定义（内容层）。
public sealed class WindBladeCardDefinition : CardDefinition
{
    public WindBladeCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(new StringName("card.wind_blade"), "风之刃",
                    new StringName("jiyun"), CardSize.Medium, [GameElements.Wind],
                    illustration: new StringName("res://art/ui/card-face/artwork/wind_blade-illustration.png"),
                    descriptionEntries:
                    [
                        new(CardKeywords.Activate, "冷却7秒，魔法消耗0，对敌方英雄造成20/40伤害。"),
                        new(CardKeywords.Echo, "当此卡牌发动后，此卡牌获得多重1（包含多重的额外发动）。"),
                    ]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 20,
                    [GameAttributeKeys.CooldownTicks] = 70,
                })),
            new TagSet([GameTags.Equipment]), initialLevel: 3,
            levels: [CreateLevel(3, 20), CreateLevel(4, 40)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_damage"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 70, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.increase_source_multicast"), AbilityActivation.EchoOnSourceCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.Multicast, 1)]),
        ]);
}
