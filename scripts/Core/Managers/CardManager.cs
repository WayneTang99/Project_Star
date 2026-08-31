using System;
using System.Reflection;
using Godot;
using Project_Star.Core.Bases;

namespace Project_Star.Core.Managers;

[GlobalClass]
public partial class CardManager : Node
{
	public Godot.Collections.Array<CardBase> CardTemplates { get; private set; } = new();

	public Godot.Collections.Array<CardBase> PlayerCards { get; private set; } = new();

	public event Action<CardBase>? CardAddedEvent;

	public override void _Ready()
	{
		base._Ready();
		RegisterCardTemplates();
	}

	public CardBase CreateCard(CardBase template)
	{
		CardBase instance = (CardBase)template.Duplicate();
		instance.AttributeSet = (CardAttributeSet)template.AttributeSet.Duplicate(true);
		AddChild(instance);
		return instance;
	}

	public void AddCardToPlayer(CardBase card)
	{
		PlayerCards.Add(card);
		CardAddedEvent?.Invoke(card);
	}

	public Godot.Collections.Array<CardBase> GetCardsByHero(string heroKey)
	{
		var result = new Godot.Collections.Array<CardBase>();
		foreach (CardBase card in PlayerCards)
		{
			if (card.AttributeSet.HeroKey == heroKey)
			{
				result.Add(card);
			}
		}

		return result;
	}

	private void RegisterCardTemplates()
	{
		foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
		{
			if (type.IsAbstract || !typeof(CardBase).IsAssignableFrom(type))
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