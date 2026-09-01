using System.Collections.Generic;
using Godot;

namespace Aria;

// 属性集合：以字典按 key 管理多个属性，提供增查改；支持修饰器的应用与回滚。
[GlobalClass]
public partial class AriaAttributeSet : Resource
{
	// 已应用修饰器记录（key → 修饰器列表，含应用前原值）
	private readonly Dictionary<string, List<AppliedModifier>> _appliedModifiers = new();

	// 属性字典（key → 属性）
	[Export]
	public Godot.Collections.Dictionary<string, AriaAttributeData> Attributes { get; set; } = new();

	// 按 key 获取属性；不存在返回 null
	public AriaAttributeData? GetAttribute(string key)
	{
		Attributes.TryGetValue(key, out AriaAttributeData? attribute);
		return attribute;
	}

	// 尝试按 key 获取属性，是否成功由返回值表示
	public bool TryGetAttribute(string key, out AriaAttributeData? attribute)
	{
		return Attributes.TryGetValue(key, out attribute);
	}

	// 添加或覆盖指定 key 的属性
	public void AddAttribute(string key, AriaAttributeData attribute)
	{
		Attributes[key] = attribute;
	}

	// 设置属性当前值；属性不存在时忽略
	public void SetAttributeValue(string key, float value)
	{
		if (GetAttribute(key) is { } attribute)
		{
			attribute.SetCurrentValue(value);
		}
	}

	// 应用修饰器：按运算方式作用于目标属性并记录原值，供移除时回滚
	public void ApplyModifier(AriaAttributeModifier modifier)
	{
		if (GetAttribute(modifier.AttributeKey) is not { } attribute)
		{
			return;
		}

		float original = attribute.CurrentValue;
		ApplyOperation(attribute, modifier.Operation, modifier.Magnitude);

		if (!_appliedModifiers.TryGetValue(modifier.AttributeKey, out List<AppliedModifier>? list))
		{
			list = new List<AppliedModifier>();
			_appliedModifiers[modifier.AttributeKey] = list;
		}

		list.Add(new AppliedModifier(modifier, original));
	}

	// 移除修饰器：回滚到应用前原值
	public void RemoveModifier(AriaAttributeModifier modifier)
	{
		if (!_appliedModifiers.TryGetValue(modifier.AttributeKey, out List<AppliedModifier>? list))
		{
			return;
		}

		for (int i = list.Count - 1; i >= 0; i--)
		{
			if (ReferenceEquals(list[i].Modifier, modifier))
			{
				if (GetAttribute(modifier.AttributeKey) is { } attribute)
				{
					attribute.SetCurrentValue(list[i].OriginalValue);
				}

				list.RemoveAt(i);
				break;
			}
		}
	}

	// 按运算方式修改属性当前值
	private void ApplyOperation(AriaAttributeData attribute, AriaAttributeOperation operation, float magnitude)
	{
		switch (operation)
		{
			case AriaAttributeOperation.Add:
				attribute.SetCurrentValue(attribute.CurrentValue + magnitude);
				break;
			case AriaAttributeOperation.Subtract:
				attribute.SetCurrentValue(attribute.CurrentValue - magnitude);
				break;
			case AriaAttributeOperation.Multiply:
				attribute.SetCurrentValue(attribute.CurrentValue * magnitude);
				break;
			case AriaAttributeOperation.Override:
				attribute.SetCurrentValue(magnitude);
				break;
		}
	}

	// 已应用修饰器记录
	private readonly struct AppliedModifier
	{
		// 修饰器
		public AriaAttributeModifier Modifier { get; }

		// 应用前原值
		public float OriginalValue { get; }

		public AppliedModifier(AriaAttributeModifier modifier, float originalValue)
		{
			Modifier = modifier;
			OriginalValue = originalValue;
		}
	}
}