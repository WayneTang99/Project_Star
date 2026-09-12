using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Pools;

namespace Project_Star.Match;

// 怪物事件子管理器：通过 PoolBase 管理怪物模板池、创建怪物实例，生命周期随 EventManager。
public sealed class MonsterEventManager
{
	private PoolBase<MonsterBase> _pool = null!;

	// 怪物模板池（反射收集，每种一张）
	public Godot.Collections.Array<MonsterBase> MonsterTemplates { get; private set; } = new();

	// 从模板复制一份怪物实例并挂载到指定父节点
	public MonsterBase CreateMonster(MonsterBase template, Node parent)
	{
		// MonsterBase 扩展 HeroBase，PoolBase 创建后挂到 _owner（EventManager 的 owner），
		// 这里额外 reparent 到事件节点下（怪物作为事件子节点）
		MonsterBase instance = _pool.CreateInstance(template);
		instance.GetParent()?.RemoveChild(instance);
		parent.AddChild(instance);
		return instance;
	}

	// 反射收集所有非抽象公开的 MonsterBase 子类作为模板
	public void RegisterTemplates(Node owner)
	{
		_pool = new PoolBase<MonsterBase>(owner);
		_pool.RegisterTemplates();

		// 同步到公开属性
		foreach (MonsterBase template in _pool.Templates)
		{
			MonsterTemplates.Add(template);
		}
	}
}
