namespace Aria;

// 最小实体接口：暴露属性集与标签集，供 Aria 层通用访问。
public interface IAriaEntity
{
	// 属性集
	AriaAttributeSet AttributeSet { get; }

	// 标签集
	AriaTagSet TagSet { get; }
}
