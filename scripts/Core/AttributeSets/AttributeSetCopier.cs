using System;
using System.Collections.Generic;
using Aria;

namespace Project_Star.Core.AttributeSets;

// 属性集深拷贝工具：Godot 引擎的 Resource.Duplicate 对自定义 C# Resource 会因 ClassDB
// 未注册该类而退化为基类 Resource 实例（类型与身份字段丢失），故属性集复制必须
// 按具体类型重建（身份字段由构造函数注入）并逐属性按值复制。
public static class AttributeSetCopier
{
	// 深拷贝属性集：重建同类型独立实例并复制全部属性值（保留目标构造函数的属性联动）。
	public static AriaAttributeSet DeepCopy(AriaAttributeSet source)
	{
		AriaAttributeSet copy = source switch
		{
			HeroAttributeSet hero => new HeroAttributeSet(hero.HeroKey, hero.HeroDisplayName),
			CardAttributeSet card => new CardAttributeSet(card.CardKey, card.DisplayName, card.HeroKey, card.Size),
			EventAttributeSet evt => new EventAttributeSet(evt.EventKey, evt.EventDisplayName),
			_ => (AriaAttributeSet)Activator.CreateInstance(source.GetType())!,
		};

		// 逐属性按值复制：目标已存在的属性原地覆盖（保留构造函数的联动），缺失的属性新增
		foreach (KeyValuePair<string, AriaAttributeData> pair in source.Attributes)
		{
			AriaAttributeData attr = pair.Value;
			if (copy.GetAttribute(pair.Key) is { } existing)
			{
				existing.BaseValue = attr.BaseValue;
				existing.SetMaxValue(attr.MaxValue);
				existing.SetMinValue(attr.MinValue);
				existing.SetCurrentValue(attr.CurrentValue);
			}
			else
			{
				copy.AddAttribute(pair.Key, new AriaAttributeData(attr.BaseValue, attr.MinValue, attr.MaxValue));
			}
		}

		return copy;
	}
}