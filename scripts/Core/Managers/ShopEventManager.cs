using System;
using System.Reflection;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Core.Managers;

// 商店事件子管理器：反射收集 MerchantBase 模板池、创建商人实例，生命周期随 EventManager。
public sealed class ShopEventManager
{
	// 商人模板池（反射收集，每种一张）
	public Godot.Collections.Array<MerchantBase> MerchantTemplates { get; private set; } = new();

	// 从模板复制一份商人实例并挂载到指定父节点
	public MerchantBase CreateMerchant(MerchantBase template, Node parent)
	{
		MerchantBase instance = (MerchantBase)template.Duplicate();
		instance.AttributeSet = (MerchantAttributeSet)template.AttributeSet.Duplicate(true);
		parent.AddChild(instance);
		return instance;
	}

	// 反射收集所有非抽象公开的 MerchantBase 子类作为模板，挂载到指定父节点
	public void RegisterTemplates(Node owner)
	{
		foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
		{
			if (type.IsAbstract || !type.IsPublic || !typeof(MerchantBase).IsAssignableFrom(type))
			{
				continue;
			}

			if (Activator.CreateInstance(type) is MerchantBase merchant)
			{
				owner.AddChild(merchant);
				MerchantTemplates.Add(merchant);
			}
		}
	}
}
