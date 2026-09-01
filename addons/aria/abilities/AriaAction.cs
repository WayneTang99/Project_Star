using System.Collections.Generic;

namespace Aria;

// 一次发动产生的动作：目标 → 效果（目标可能多个）。
public class AriaAction
{
	// 目标到效果列表的映射
	public Dictionary<IAriaEntity, AriaEffectBase[]> EffectsByTarget { get; set; } = new();
}