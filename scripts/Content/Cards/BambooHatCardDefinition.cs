using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 斗笠极云卡牌内容定义（内容层）。
public sealed class BambooHatCardDefinition : CardDefinition
{
    public BambooHatCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.bamboo_hat"), "斗笠", new StringName("jiyun"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/bamboo_hat-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却7秒，魔法消耗0，获得40/60/80/100护甲。"),
                    new(CardKeywords.Echo, "当己方造成迟缓后，此卡牌获得1秒充能。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 70,
                [GameAttributeKeys.Armor] = 40,
            })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 40), CreateLevel(2, 60), CreateLevel(3, 80), CreateLevel(4, 100)]) { }

    private static CardLevelDefinition CreateLevel(int level, int armor) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Armor] = armor },
        [
            new AbilityDefinition(new StringName("ability.gain_armor"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 70, [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)]),
            new AbilityDefinition(new StringName("ability.charge_on_allied_slow"), AbilityActivation.EchoOnAlliedSlowApplied,
                AbilityTarget.SelfCard, 0, 0, [new ChargeSourceCardEffectDefinition(10)]),
        ]);
}
