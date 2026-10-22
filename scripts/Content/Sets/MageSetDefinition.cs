using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Sets;

// 魔导士五件装备的再生与消耗返还套装（内容层）。
public sealed class MageSetDefinition : CardSetDefinition
{
    public MageSetDefinition() : base(new EntityAttributes<CardSetIdentityAttributes>(
        new CardSetIdentityAttributes(new StringName("set.mage"), "魔导士")),
        [new CardSetThresholdDefinition(3,
            [new AbilityDefinition(new StringName("ability.mage_regeneration"), AbilityActivation.PassiveAura,
                AbilityTarget.AlliedHero, 0, 0, [new ManaRegenAuraEffectDefinition(40)])]),
         new CardSetThresholdDefinition(5,
            [new AbilityDefinition(new StringName("ability.mage_mana_refund"), AbilityActivation.EchoOnAlliedManaSpent,
                AbilityTarget.AlliedHero, 0, 0, [new RestoreEventManaSpentEffectDefinition()])])]) { }
}
