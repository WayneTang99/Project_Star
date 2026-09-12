using System;
using Aria;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Pools;
using Project_Star.Core.Types;
using Project_Star.Match.Events;

namespace Project_Star.Systems;

// 卡牌管理器：通过 PoolBase 管理模板池，复制实例、管理玩家拥有的卡牌并支持按阵营过滤。
[GlobalClass]
public partial class CardManager : GlobalManagerBase
{
	private PoolBase<CardBase> _pool = null!;

	// 全部卡牌模板（反射收集，每种一张）
	public Godot.Collections.Array<CardBase> CardTemplates { get; private set; } = new();

	// 玩家当前拥有的卡牌
	public Godot.Collections.Array<CardBase> PlayerCards { get; private set; } = new();

	// 卡牌加入玩家事件
	public event Action<CardBase>? CardAddedEvent;

	protected override void OnInitialize()
	{
		base.OnInitialize();

		_pool = new PoolBase<CardBase>(this);
		_pool.RegisterTemplates();

		// 同步到公开属性供外部读取
		foreach (CardBase template in _pool.Templates)
		{
			CardTemplates.Add(template);
		}
	}

	// 从模板复制一份卡牌实例并挂载为本节点子节点
	public CardBase CreateCard(CardBase template)
	{
		return _pool.CreateInstance(template);
	}

	// 将卡牌加入玩家拥有的卡池并广播事件。
	// 加入卡池即视为获得，价值统一按当前初始价值折半（与获得途径无关），
	// 使后续增值/减值事件（叠加逻辑）始终在一致的半价基线上进行。
	public void AddCardToPlayer(CardBase card)
	{
		card.AttributeSet.Value.SetCurrentValue(card.GetInitialValue() * CardEconomy.OBTAINED_VALUE_RATIO);
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

	// 购买卡牌：财富充足则按初始价值扣财富、从模板复制独立实例并加入玩家卡池后广播事件；否则不执行。
	// 传入的 card 为模板，模板本身永不加入玩家，仅加入其独立副本（模板池保持不变）。
	// 成交价 = 初始价值（价值系数 * 等级 * 型号系数）；加入卡池后的价值折半见 AddCardToPlayer。
	public bool BuyCard(CardBase card, AriaAttributeData wealth)
	{
		float price = card.GetInitialValue();
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

	// 出售卡牌：从玩家卡池移除并按现值全额回补财富，广播事件；未持有则不执行。
	// 购买折半已计入副本 Value（现值），此处不再二次打折。
	public bool SellCard(CardBase card, AriaAttributeData wealth)
	{
		if (!RemoveCardFromPlayer(card))
		{
			return false;
		}

		wealth.SetCurrentValue(wealth.CurrentValue + card.AttributeSet.Value.CurrentValue);
		MatchEventBus.Raise(new ItemSoldEvent(card));
		return true;
	}
}
