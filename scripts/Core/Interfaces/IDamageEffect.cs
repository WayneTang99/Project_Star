namespace Project_Star.Core.Interfaces;

// 伤害效果接口：结算器据此识别伤害效果并广播 DamageDealt。
public interface IDamageEffect
{
	// 伤害量（结算前）
	float DamageAmount { get; }

	// 是否穿透（无视护甲）
	bool IsPiercing { get; }

	// 本次结算护甲吸收量（Apply 后写入）
	float LastAbsorbedByArmor { get; }
}