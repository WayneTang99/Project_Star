using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Events;

// 伤害事件载荷：一次伤害结算的完整信息（结算后由结算器广播）。
public struct DamageInfo
{
	// 来源
	public ICombatant? Source;

	// 目标
	public ICombatant Target;

	// 结算前伤害量
	public float Amount;

	// 是否穿透（无视护甲）
	public bool IsPiercing;

	// 护甲吸收量
	public float AbsorbedByArmor;

	// 实际扣除生命量
	public float DamageToHealth => Amount - AbsorbedByArmor;
}