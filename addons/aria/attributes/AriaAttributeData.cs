using System;
using Godot;

namespace Aria;

[GlobalClass]
public partial class AriaAttributeData : Resource
{
	[Export]
	public float BaseValue { get; set; }

	[Export]
	public float CurrentValue { get; set; }

	[Export]
	public float MinValue { get; set; }

	[Export]
	public float MaxValue { get; set; }

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

	public void SetCurrentValue(float value)
	{
		ApplyValue(Mathf.Clamp(value, MinValue, MaxValue));
	}

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