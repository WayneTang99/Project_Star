using System.Collections.Generic;
using Godot;

namespace Aria;

// 标签集合：轻量级标签容器，供实体挂载词条/标签，无触发逻辑。
public class AriaTagSet
{
	private readonly HashSet<StringName> _tags = new();

	// 标签数量
	public int Count => _tags.Count;

	// 是否为空
	public bool IsEmpty => _tags.Count == 0;

	// 所有标签
	public IEnumerable<StringName> All => _tags;

	// 是否包含指定标签
	public bool Has(StringName tag) => _tags.Contains(tag);

	// 添加标签
	public void Add(StringName tag) => _tags.Add(tag);

	// 移除标签，返回是否成功
	public bool Remove(StringName tag) => _tags.Remove(tag);

	// 清空所有标签
	public void Clear() => _tags.Clear();

	// 合并另一个标签集合
	public void Merge(AriaTagSet other)
	{
		foreach (StringName tag in other._tags)
		{
			_tags.Add(tag);
		}
	}
}
