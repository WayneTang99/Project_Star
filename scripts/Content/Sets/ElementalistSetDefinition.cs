using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Sets;

// 元素使三件套按战场不同元素数量缩减己方冷却（内容层）。
public sealed class ElementalistSetDefinition : CardSetDefinition
{
    public ElementalistSetDefinition() : base(new EntityAttributes<CardSetIdentityAttributes>(
        new CardSetIdentityAttributes(new StringName("set.elementalist"), "元素使")),
        [new CardSetThresholdDefinition(3,
            [new AbilityDefinition(new StringName("ability.cooldown_per_allied_element"), AbilityActivation.PassiveAura,
                AbilityTarget.AlliedHero, 0, 0, [new ReduceAlliedCooldownPerElementAuraEffectDefinition(2)])])]) { }
}
