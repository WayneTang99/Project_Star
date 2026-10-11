using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Sets;

// 冒险者套装的累积阈值定义（内容层）。
public sealed class AdventurerSetDefinition : CardSetDefinition
{
    public AdventurerSetDefinition() : base(new EntityAttributes<CardSetIdentityAttributes>(
        new CardSetIdentityAttributes(new StringName("set.adventurer"), "冒险者")),
        [
            new CardSetThresholdDefinition(2,
                [new AbilityDefinition(new StringName("ability.adventurer_round_experience"), AbilityActivation.PassiveOnRoundStart,
                    AbilityTarget.AlliedHero, 0, 0, [new GrantExperienceEffectDefinition(1)])]),
            new CardSetThresholdDefinition(3,
                [new AbilityDefinition(new StringName("ability.adventurer_victory_experience"), AbilityActivation.PassiveOnBattleVictory,
                    AbilityTarget.AlliedHero, 0, 0, [new GrantExperienceEffectDefinition(1)])]),
            new CardSetThresholdDefinition(5,
                [new AbilityDefinition(new StringName("ability.adventurer_max_health"), AbilityActivation.PassiveWhileEnabled,
                    AbilityTarget.AlliedHero, 0, 0, [new ModifyHeroLevelScaledAttributeEffectDefinition(GameAttributeKeys.MaxHealth, 10)])]),
        ]) { }
}
