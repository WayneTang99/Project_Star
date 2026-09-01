using System;
using Godot;

namespace Aria;

// 单个属性：Base / Current / Min / Max 值，值变更事件，Min/Max 联动校正。
[GlobalClass]
public partial class AriaAttributeData : Resource
{
	// 基础值
	[Export]
	public float BaseValue { get; set; }

	// 当前值
	[Export]
	public float CurrentValue { get; set; }

	// 最小值
	[Export]
	public float MinValue { get; set; }

	// 最大值
	[Export]
	public float MaxValue { get; set; }

	// 值变更事件，参数为属性与变化量 delta
	public event Action<AriaAttributeData, float>? OnValueChanged;

	public AriaAttributeData()
	{
	}

	public AriaAttributeData(float baseValue, float minValue, float maxValue)
	{
		BaseValue = baseValue;
		CurrentValue = baseValue;
		MinValue = minValue;
		MaxValue = maxValue;
	}

	// 设置当前值，自动夹取到 [Min, Max]
	public void SetCurrentValue(float value)
	{
		ApplyValue(Mathf.Clamp(value, MinValue, MaxValue));
	}

	// 设置最小值，联动校正 Base 与 Current 不越界
	public void SetMinValue(float minValue)
	{
		if (minValue > MaxValue)
		{
			minValue = MaxValue;
		}

		MinValue = minValue;
		BaseValue = Mathf.Max(BaseValue, MinValue);
		ApplyValue(Mathf.Clamp(CurrentValue, MinValue, MaxValue));
	}

	// 设置最大值，联动校正 Base 与 Current 不越界
	public void SetMaxValue(float maxValue)
	{
		if (maxValue < MinValue)
		{
			maxValue = MinValue;
		}

		MaxValue = maxValue;
		BaseValue = Mathf.Min(BaseValue, MaxValue);
		ApplyValue(Mathf.Clamp(CurrentValue, MinValue, MaxValue));
	}

	// 写入新值：与当前值近似相等则忽略，否则更新并广播变更事件
	private void ApplyValue(float newValue)
	{
		if (Mathf.IsEqualApprox(newValue, CurrentValue))
		{
			return;
		}

		float delta = newValue - CurrentValue;
		CurrentValue = newValue;
		OnValueChanged?.Invoke(this, delta);
	}
}