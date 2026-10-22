using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Sets;

// 巫师套装的暗属性冷却光环与召唤充能（内容层）。
public sealed class WizardSetDefinition : CardSetDefinition
{
    public WizardSetDefinition() : base(new EntityAttributes<CardSetIdentityAttributes>(
        new CardSetIdentityAttributes(new StringName("set.wizard"), "巫师")),
        [new CardSetThresholdDefinition(2,
            [new AbilityDefinition(new StringName("ability.reduce_allied_dark_cooldown"), AbilityActivation.PassiveAura,
                AbilityTarget.AlliedHero, 0, 0, [new ReduceAlliedElementCardCooldownAuraEffectDefinition(GameElements.Dark, 5)])]),
         new CardSetThresholdDefinition(3,
            [new AbilityDefinition(new StringName("ability.charge_random_ally_on_summon"), AbilityActivation.EchoOnAlliedCardSummoned,
                AbilityTarget.AlliedHero, 0, 0, [new ChargeRandomOtherAlliedCardsEffectDefinition(10, 1, ExcludeSource: false)])])]) { }
}
