using System;
using System.Reflection;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Core.Managers;

// 怪物事件子管理器：反射收集 MonsterBase 模板池、创建怪物实例，生命周期随 EventManager。
public sealed class MonsterEventManager
{
	// 怪物模板池（反射收集，每种一张）
	public Godot.Collections.Array<MonsterBase> MonsterTemplates { get; private set; } = new();

	// 从模板复制一份怪物实例并挂载到指定父节点
	public MonsterBase CreateMonster(MonsterBase template, Node parent)
	{
		MonsterBase instance = (MonsterBase)template.Duplicate();
		instance.AttributeSet = (HeroAttributeSet)template.AttributeSet.Duplicate(true);
		parent.AddChild(instance);
		return instance;
	}

	// 反射收集所有非抽象公开的 MonsterBase 子类作为模板，挂载到指定父节点
	public void RegisterTemplates(Node owner)
	{
		foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
		{
			if (type.IsAbstract || !type.IsPublic || !typeof(MonsterBase).IsAssignableFrom(type))
			{
				continue;
			}

			if (Activator.CreateInstance(type) is MonsterBase monster)
			{
				owner.AddChild(monster);
				MonsterTemplates.Add(monster);
			}
		}
	}
}
