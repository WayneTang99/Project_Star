using Godot;

namespace Aria;

[GlobalClass]
public partial class AriaAttributeSet : Resource
{
	[Export]
	public Godot.Collections.Dictionary<string, AriaAttributeData> Attributes { get; set; } = new();

	public AriaAttributeData? GetAttribute(string key)
	{
		Attributes.TryGetValue(key, out AriaAttributeData? attribute);
		return attribute;
	}

	public bool TryGetAttribute(string key, out AriaAttributeData? attribute)
	{
		return Attributes.TryGetValue(key, out attribute);
	}

	public void AddAttribute(string key, AriaAttributeData attribute)
	{
		Attributes[key] = attribute;
	}

	public void SetAttributeValue(string key, float value)
	{
		if (GetAttribute(key) is { } attribute)
		{
			attribute.SetCurrentValue(value);
		}
	}
}