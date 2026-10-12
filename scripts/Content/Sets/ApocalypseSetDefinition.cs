using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Sets;

// 阈值累计生效，战斗能力按开战套装快照冻结。
public sealed class ApocalypseSetDefinition : CardSetDefinition
{
    public ApocalypseSetDefinition() : base(new EntityAttributes<CardSetIdentityAttributes>(
        new CardSetIdentityAttributes(new StringName("set.apocalypse"), "天启")),
        [
            new CardSetThresholdDefinition(2,
                [new AbilityDefinition(new StringName("ability.set_round_cooldown"), AbilityActivation.PassiveOnRoundStart,
                    AbilityTarget.AlliedHero, 0, 0, [new ReduceSetCardCooldownEffectDefinition(2)])]),
            new CardSetThresholdDefinition(3,
                [new AbilityDefinition(new StringName("ability.eclipse_max_health"), AbilityActivation.PassiveOnEclipseStart,
                    AbilityTarget.AlliedHero, 0, 0, [new IncreaseHeroMaxHealthPercentEffectDefinition(50)])]),
            new CardSetThresholdDefinition(4,
                [new AbilityDefinition(new StringName("ability.eclipse_enemy_damage"), AbilityActivation.PassiveAura,
                    AbilityTarget.EnemyHero, 0, 0, [new MultiplyReceivedEclipseDamageEffectDefinition(2)])]),
        ]) { }
}
