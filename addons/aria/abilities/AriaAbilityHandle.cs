using Godot;

namespace Aria;

// 能力运行期句柄：含冷却计时与所属实体，由管理器统一驱动。
public partial class AriaAbilityHandle : Resource
{
	// 能力定义
	public AriaAbilityBase Definition { get; }

	// 所属实体（IAriaEntity 为 Aria 接口，保持游戏无关）
	public IAriaEntity? Owner { get; }

	// 剩余冷却
	public float CooldownRemaining { get; private set; }

	// 是否就绪（冷却已到 0）
	public bool IsReady => CooldownRemaining <= 0f;

	// 构造：绑定能力定义与所属实体
	public AriaAbilityHandle(AriaAbilityBase definition, IAriaEntity? owner)
	{
		Definition = definition;
		Owner = owner;
	}

	// 冷却递减，下限为 0
	public void UpdateCooldown(float delta)
	{
		CooldownRemaining = Mathf.Max(0f, CooldownRemaining - delta);
	}

	// 发动后进入冷却；无冷却能力保持 0
	public void StartCooldown()
	{
		CooldownRemaining = Definition.HasCooldown ? Definition.GetCooldownSeconds() : 0f;
	}
}
