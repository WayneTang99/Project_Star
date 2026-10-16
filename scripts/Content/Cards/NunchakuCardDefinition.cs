using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 双截棍极云卡牌内容定义（内容层）。
public sealed class NunchakuCardDefinition : CardDefinition
{
    public NunchakuCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.nunchaku"), "双截棍", new StringName("jiyun"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/nunchaku-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却7秒，魔法消耗0，多重1，对敌方英雄造成5伤害。"),
                    new(CardKeywords.Echo, "当己方相邻战场卡牌发动后，此卡牌获得1/2/3秒充能；多重发动分别触发。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 5,
                    [GameAttributeKeys.CooldownTicks] = 70,
                    [GameAttributeKeys.Multicast] = 1,
                })), new TagSet([GameTags.Equipment]), initialLevel: 2,
            levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 30)]) { }

    private static CardLevelDefinition CreateLevel(int level, int chargeTicks) => new(level, null,
        [
            new AbilityDefinition(new StringName("ability.attribute_damage"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 70, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            ChargeOnActivation(AdjacentCardSide.Left, "ability.charge_on_left_activation", chargeTicks),
            ChargeOnActivation(AdjacentCardSide.Right, "ability.charge_on_right_activation", chargeTicks),
        ]);

    private static AbilityDefinition ChargeOnActivation(AdjacentCardSide side, StringName key, int ticks) => new(key,
        AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
        [new ChargeSourceCardEffectDefinition(ticks)], triggerCardSide: side);
}
