using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 霜灵鹿莫娜卡牌，随机禁锢并响应己方冰属性发动充能。
public sealed class FrostSpiritDeerCardDefinition : CardDefinition
{
    public FrostSpiritDeerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.frost_spirit_deer"), "霜灵鹿", new StringName("mona"), CardSize.Medium, [GameElements.Ice],
            illustration: new StringName("res://art/ui/card-face/artwork/frost_spirit_deer-illustration-20261022.png"),
            descriptionEntries: [new(CardKeywords.Activate, "给予敌方随机1张卡牌1秒禁锢。"),
                new(CardKeywords.Echo, "己方冰属性卡牌发动时，此卡牌获得1秒充能。")]),
            baseCombat: new ModifiableAttributeSet()), new TagSet([GameTags.Beast, GameTags.Spirit]), initialLevel: 3,
            levels: [CreateLevel(3, 80), CreateLevel(4, 70)]) { }

    private static CardLevelDefinition CreateLevel(int level, int cooldown) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = cooldown },
        [new AbilityDefinition(new StringName("ability.random_immobilize"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, cooldown,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, 10)]),
         new AbilityDefinition(new StringName("ability.charge_on_ice_activation"),
            AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new ChargeSourceCardEffectDefinition(10)], triggerCardElement: GameElements.Ice)]);
}
