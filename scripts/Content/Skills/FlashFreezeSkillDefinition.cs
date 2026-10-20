using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Skills;

// 急冻小魔女技能，复用一次性元素发动联动窗口（内容层）。
public sealed class FlashFreezeSkillDefinition : SkillDefinition
{
    public FlashFreezeSkillDefinition()
        : base(new EntityAttributes<SkillIdentityAttributes>(new SkillIdentityAttributes(
            new StringName("skill.flash_freeze"), "急冻", new StringName("mona"),
            illustration: new StringName("res://art/ui/skills/artwork/flash_freeze-illustration.png"))),
            initialLevel: 3, levels: [CreateLevel(3), CreateLevel(4)]) { }

    private static SkillLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.flash_freeze_sequence"), AbilityActivation.PassiveAura,
            AbilityTarget.EnemyHero, 0, 0,
            [new ElementSequenceEffectDefinition(GameElements.Water, GameElements.Ice,
                (level - 2) * 10, Status: BattleStatus.ImmobilizeDuration, StatusTicks: 10)])]);
}
