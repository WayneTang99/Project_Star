namespace Project_Star.Core.Interfaces;

// 治疗效果接口：结算器据此识别治疗效果，触发净化（削减目标腐蚀 / 辐射）。
public interface IHealEffect
{
	// 治疗量（净化总量 = 治疗量 × 50%）
	float HealAmount { get; }
}