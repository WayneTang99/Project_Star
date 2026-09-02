namespace Project_Star.Combat.Events;

// 伤害结算事件：携带完整伤害信息。
public class DamageDealtEvent : CombatEventBase
{
	// 伤害信息
	public DamageInfo Info { get; }

	public DamageDealtEvent(DamageInfo info)
	{
		Info = info;
	}
}