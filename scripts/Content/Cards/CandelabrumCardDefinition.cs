using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 烛台莫娜卡牌定义，火属性发动回响获得疾速（内容层）。
public sealed class CandelabrumCardDefinition : CardDefinition
{
    public CandelabrumCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.candelabrum"), "烛台", new StringName("mona"), CardSize.Small, [GameElements.Fire],
            illustration: new StringName("res://art/ui/card-face/artwork/candelabrum-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "对敌方英雄施加10/20/40/80灼伤。"),
                new(CardKeywords.Echo, "己方火属性卡牌发动时，此卡牌获得1秒疾速。"),
                new(CardKeywords.Consume, "消耗。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 100 })),
            new TagSet([GameTags.Consumable]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int burn) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Burn] = burn },
        [new AbilityDefinition(new StringName("ability.attribute_burn_and_consume"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 100,
            [new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn), new DestroyCardEffectDefinition(false)]),
         new AbilityDefinition(new StringName("ability.haste_on_allied_fire_activation"),
            AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)], triggerCardElement: GameElements.Fire)]);
}
