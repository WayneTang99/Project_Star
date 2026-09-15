using Godot;

namespace Project_Star.Core.States;

// 战斗内临时状态容器（战斗层）：每场战斗开始派生初始值、结束丢弃，数值以 StringName 为 key 存入字典。
public abstract class BattleState : StateContainer
{
    // 深拷贝战斗状态（供战斗初始值派生时调用）。
    public override StateContainer Clone()
    {
        var copy = (BattleState)MemberwiseClone();
        return copy;
    }
}
