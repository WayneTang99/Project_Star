using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Pools;

namespace Project_Star.Match;

// 商店事件子管理器：通过 PoolBase 管理商人模板池、创建商人实例，生命周期随 EventManager。
public sealed class ShopEventManager
{
	private PoolBase<MerchantBase> _pool = null!;

	// 商人模板池（反射收集，每种一张）
	public Godot.Collections.Array<MerchantBase> MerchantTemplates { get; private set; } = new();

	// 从模板复制一份商人实例并挂载到指定父节点
	public MerchantBase CreateMerchant(MerchantBase template, Node parent)
	{
		MerchantBase instance = _pool.CreateInstance(template);
		instance.GetParent()?.RemoveChild(instance);
		parent.AddChild(instance);
		return instance;
	}

	// 反射收集所有非抽象公开的 MerchantBase 子类作为模板
	public void RegisterTemplates(Node owner)
	{
		_pool = new PoolBase<MerchantBase>(owner);
		_pool.RegisterTemplates();

		foreach (MerchantBase template in _pool.Templates)
		{
			MerchantTemplates.Add(template);
		}
	}
}
