using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 冰火精灵莫娜卡牌定义，其他冰属性发动成长、火属性发动充能（内容层）。
public sealed class IcefireSpiritCardDefinition : CardDefinition
{
    public IcefireSpiritCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.icefire_spirit"), "冰火精灵", new StringName("mona"), CardSize.Small,
            [GameElements.Ice, GameElements.Fire],
            illustration: new StringName("res://art/ui/card-face/artwork/icefire_spirit-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "给予敌方随机1张卡牌1秒禁锢，对敌方英雄施加6灼伤。"),
                new(CardKeywords.Echo, "己方其他冰属性卡牌发动时，此卡牌灼伤增加4/8。"),
                new(CardKeywords.Echo, "己方其他火属性卡牌发动时，此卡牌获得1秒充能。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 80, [GameAttributeKeys.Burn] = 6 })),
            new TagSet([GameTags.Spirit]), initialLevel: 3, levels: [CreateLevel(3, 4), CreateLevel(4, 8)]) { }

    private static CardLevelDefinition CreateLevel(int level, int growth) => new(level, null,
        [new AbilityDefinition(new StringName("ability.random_immobilize_and_burn"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 80,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, 10),
                new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)]),
         new AbilityDefinition(new StringName("ability.grow_burn_on_other_ice_activation"),
            AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.Burn, growth)],
            triggerCardElement: GameElements.Ice, excludeSourceCard: true),
         new AbilityDefinition(new StringName("ability.charge_on_other_fire_activation"),
            AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new ChargeSourceCardEffectDefinition(10)], triggerCardElement: GameElements.Fire, excludeSourceCard: true)]);
}
