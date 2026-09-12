using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using Aria;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Core.Pools;

// 泛型实体池基类：反射收集模板、创建实例、跟踪拥有。
// 消除 HeroManager / CardManager / MonsterEventManager / EventManager 中重复的反射模板收集逻辑。
public class PoolBase<T> where T : Node
{
	private readonly Node _owner;

	// 模板池（反射收集，每种一个）
	public List<T> Templates { get; } = new();

	// 已创建的实例
	private readonly List<T> _instances = new();

	// 已创建实例的只读视图
	public IReadOnlyList<T> Instances => _instances;

	public PoolBase(Node owner)
	{
		_owner = owner;
	}

	// 反射收集所有非抽象公开的 T 子类作为模板
	public void RegisterTemplates()
	{
		foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
		{
			if (type.IsAbstract || !type.IsPublic || !typeof(T).IsAssignableFrom(type))
			{
				continue;
			}

			if (Activator.CreateInstance(type) is T template)
			{
				_owner.AddChild(template);
				Templates.Add(template);
			}
		}
	}

	// 从模板创建一份实例（Duplicate + DeepCopy AttributeSet），挂载到 owner
	public T CreateInstance(T template)
	{
		T instance = (T)template.Duplicate();
		DeepCopyAttributeSet(template, instance);
		_owner.AddChild(instance);
		_instances.Add(instance);
		return instance;
	}

	// 深拷贝属性集：根据实体类型调用 AttributeSetCopier 并赋值给实例
	private static void DeepCopyAttributeSet(T source, T target)
	{
		if (source is HeroBase heroSrc && target is HeroBase heroTgt)
		{
			heroTgt.AttributeSet = (HeroAttributeSet)AttributeSetCopier.DeepCopy(heroSrc.AttributeSet);
		}
		else if (source is CardBase cardSrc && target is CardBase cardTgt)
		{
			cardTgt.AttributeSet = (CardAttributeSet)AttributeSetCopier.DeepCopy(cardSrc.AttributeSet);
		}
		else if (source is EventBase evtSrc && target is EventBase evtTgt)
		{
			evtTgt.AttributeSet = (EventAttributeSet)AttributeSetCopier.DeepCopy(evtSrc.AttributeSet);
		}
		else if (source is MonsterBase monsterSrc && target is MonsterBase monsterTgt)
		{
			monsterTgt.AttributeSet = (HeroAttributeSet)AttributeSetCopier.DeepCopy(monsterSrc.AttributeSet);
		}
	}

	// 释放指定实例
	public void FreeInstance(T instance)
	{
		instance.QueueFree();
		_instances.Remove(instance);
	}

	// 释放所有实例
	public void FreeAllInstances()
	{
		foreach (T instance in _instances)
		{
			if (GodotObject.IsInstanceValid(instance))
			{
				instance.QueueFree();
			}
		}

		_instances.Clear();
	}

	// 按条件过滤模板
	public List<T> FilterTemplates(Func<T, bool> predicate)
	{
		var result = new List<T>();
		foreach (T template in Templates)
		{
			if (predicate(template))
			{
				result.Add(template);
			}
		}

		return result;
	}

	// 随机选取一个模板
	public T? GetRandomTemplate()
	{
		if (Templates.Count == 0) return null;
		return Templates[GD.RandRange(0, Templates.Count - 1)];
	}
}
