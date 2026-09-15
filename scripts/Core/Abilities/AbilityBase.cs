using Godot;
using Project_Star.Core.States;

namespace Project_Star.Core.Abilities;

// 能力抽象基类（核心层）：实现 IAbility，提供默认能量检查与属性自动实现。
// 具体能力继承此基类，override Execute 实现逻辑；CanActivate 可选覆写扩展。
// 能力不感知等级；卡牌根据等级设置属性值，等级变化时自动更新。
public abstract class AbilityBase : IAbility
{
    public required StringName AbilityKey { get; init; }
    public required string DisplayName { get; init; }
    public AbilityTrigger Trigger { get; init; }
    public int EnergyCost { get; init; }
    public int Cooldown { get; init; }

    // 默认检查：能量是否足够。子类覆写时须 base.CanActivate && 自定义条件。
    public virtual bool CanActivate(object caster)
    {
        if (caster is HeroBattleState hero)
            return hero.Energy >= EnergyCost;
        return true;
    }

    // 执行能力逻辑（子类实现）。
    public abstract void Execute(object context);
}
