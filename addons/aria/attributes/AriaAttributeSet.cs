using Godot;

namespace Aria;

// 属性集合：以字典按 key 管理多个属性，提供增查改。
[GlobalClass]
public partial class AriaAttributeSet : Resource
{
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
}