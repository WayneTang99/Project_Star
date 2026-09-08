using System;
using System.Reflection;
using Aria;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Match.Events;

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
		instance.AttributeSet = (CardAttributeSet)AttributeSetCopier.DeepCopy(template.AttributeSet);
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

	// 按阵营 key 过滤卡牌模板池，供商店生成可购买卡牌（区别于 GetCardsByFaction，后者过滤玩家卡牌）
	public Godot.Collections.Array<CardBase> GetCardTemplatesByFaction(StringName factionKey)
	{
		var result = new Godot.Collections.Array<CardBase>();
		foreach (CardBase card in CardTemplates)
		{
			if (card.AttributeSet.HeroKey == factionKey)
			{
				result.Add(card);
			}
		}

		return result;
	}

	// 购买卡牌：财富充足则扣财富、从模板复制独立实例并加入玩家卡池后广播事件；否则不执行。
	// 传入的 card 为模板，模板本身永不加入玩家，仅加入其独立副本（模板池保持不变）。
	public bool BuyCard(CardBase card, AriaAttributeData wealth)
	{
		float price = card.AttributeSet.Value.CurrentValue;
		if (wealth.CurrentValue < price)
		{
			return false;
		}

		wealth.SetCurrentValue(wealth.CurrentValue - price);
		CardBase instance = CreateCard(card);
		AddCardToPlayer(instance);
		MatchEventBus.Raise(new ItemPurchasedEvent(instance));
		return true;
	}

	// 将卡牌从玩家卡池移除，若存在返回 true
	public bool RemoveCardFromPlayer(CardBase card)
	{
		if (PlayerCards.Contains(card))
		{
			PlayerCards.Remove(card);
			return true;
		}

		return false;
	}

	// 出售卡牌：从玩家卡池移除并按 0.5 倍价值回补财富，广播事件；未持有则不执行
	public bool SellCard(CardBase card, AriaAttributeData wealth)
	{
		if (!RemoveCardFromPlayer(card))
		{
			return false;
		}

		wealth.SetCurrentValue(wealth.CurrentValue + 0.5f * card.AttributeSet.Value.CurrentValue);
		MatchEventBus.Raise(new ItemSoldEvent(card));
		return true;
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