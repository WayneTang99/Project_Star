namespace Aria;

// 最小实体接口：暴露属性集，供 Aria 层通用访问。
public interface IAriaEntity
{
	// 属性集
	AriaAttributeSet AttributeSet { get; }
}
