using System;
using System.Reflection;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;

namespace Project_Star.Systems;

// 卡牌管理器：反射收集模板池、复制实例、管理玩家拥有的卡牌并支持按阵营过滤。
[GlobalClass]
public partial class CardManager : Node
{
	// 全部卡牌模板（反射收集，每种一张）
	public Godot.Collections.Array<CardBase> CardTemplates { get; private set; } = new();

	// 玩家当前拥有的卡牌
	public Godot.Collections.Array<CardBase> PlayerCards { get; private set; } = new();

	// 卡牌加入玩家事件
	public event Action<CardBase>? CardAddedEvent;

	public override void _Ready()
	{
		base._Ready();
		RegisterCardTemplates();
	}

	// 从模板复制一份卡牌实例并挂载为本节点子节点
	public CardBase CreateCard(CardBase template)
	{
		CardBase instance = (CardBase)template.Duplicate();
		instance.AttributeSet = (CardAttributeSet)template.AttributeSet.Duplicate(true);
		AddChild(instance);
		return instance;
	}

	// 将卡牌加入玩家拥有的卡池并广播事件
	public void AddCardToPlayer(CardBase card)
	{
		PlayerCards.Add(card);
		CardAddedEvent?.Invoke(card);
	}

	// 按阵营 key 过滤玩家卡牌，供商店等系统使用
	public Godot.Collections.Array<CardBase> GetCardsByFaction(StringName factionKey)
	{
		var result = new Godot.Collections.Array<CardBase>();
		foreach (CardBase card in PlayerCards)
		{
			if (card.AttributeSet.HeroKey == factionKey)
			{
				result.Add(card);
			}
		}

		return result;
	}

	// 反射收集所有非抽象公开的 CardBase 子类作为模板
	private void RegisterCardTemplates()
	{
		foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
		{
			if (type.IsAbstract || !type.IsPublic || !typeof(CardBase).IsAssignableFrom(type))
			{
				continue;
			}

			if (Activator.CreateInstance(type) is CardBase card)
			{
				AddChild(card);
				CardTemplates.Add(card);
			}
		}
	}
}