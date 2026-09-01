namespace Aria;

// 效果持续类型。
public enum AriaEffectDurationType
{
	// 即时生效：应用一次立即结算，不驻留
	Instant,
	// 持续：驻留 DurationSeconds，到期自动移除（可含周期 Tick）
	HasDuration,
	// 永久：应用后一直驻留，直到显式移除
	Permanent,
}