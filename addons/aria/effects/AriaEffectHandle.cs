using Godot;

namespace Aria;

// 效果运行期句柄：记录效果挂载信息与计时，由管理器统一驱动（计时不落在效果实例上）。
public partial class AriaEffectHandle : Resource
{
	// 效果定义
	public AriaEffectBase Definition { get; }

	// 所属实体（IAriaEntity 为 Aria 接口，保持游戏无关）
	public IAriaEntity? Owner { get; }

	// 目标实体
	public IAriaEntity? Target { get; }

	// 剩余持续时长（Permanent 视为无穷）
	public float RemainingDuration { get; private set; }

	// 距上次周期结算的累计时间
	public float ElapsedSinceTick { get; private set; }

	// 是否到期（有周期且累计时间达到周期）
	public bool IsTickDue => Definition.PeriodSeconds > 0f && ElapsedSinceTick >= Definition.PeriodSeconds;

	// 是否已过期（剩余时长为 0）
	public bool IsExpired => RemainingDuration <= 0f;

	// 构造：绑定效果定义与所属/目标实体，初始化持续时长
	public AriaEffectHandle(AriaEffectBase definition, IAriaEntity? owner, IAriaEntity? target)
	{
		Definition = definition;
		Owner = owner;
		Target = target;
		RemainingDuration = definition.DurationType == AriaEffectDurationType.Permanent
			? float.PositiveInfinity
			: definition.DurationSeconds;
	}

	// 推进时间：累计周期时间并递减剩余时长
	public void AdvanceTime(float delta)
	{
		ElapsedSinceTick += delta;
		RemainingDuration -= delta;
	}

	// 本次周期结算完成，清空累计时间
	public void ResetTickTimer()
	{
		ElapsedSinceTick = 0f;
	}
}