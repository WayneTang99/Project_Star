using System;
using System.Reflection;
using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.Core.Managers;

[GlobalClass]
public partial class HeroManager : Node
{
	private GameManager _gameManager = null!;

	public Godot.Collections.Array<HeroBase> AvailableHeroes { get; private set; } = new();

	public HeroBase? CurrentHero { get; private set; }

	public event Action<HeroBase>? HeroSelectedEvent;

	public event Action? HeroResetEvent;

	public override void _Ready()
	{
		base._Ready();
		_gameManager = GetNode<GameManager>("../GameManager");
		_gameManager.StateChangedEvent += OnStateChanged;
		RegisterHeroTemplates();
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		if (_gameManager is not null)
		{
			_gameManager.StateChangedEvent -= OnStateChanged;
		}
	}

	public void SelectHero(HeroBase template)
	{
		HeroBase instance = (HeroBase)template.Duplicate();
		instance.AttributeSet = (HeroAttributeSet)template.AttributeSet.Duplicate(true);
		AddChild(instance);

		CurrentHero = instance;
		HeroSelectedEvent?.Invoke(instance);
		_gameManager.HeroSelected();
	}

	public void ResetHero()
	{
		if (CurrentHero is not null)
		{
			CurrentHero.QueueFree();
		}

		CurrentHero = null;
		HeroResetEvent?.Invoke();
	}

	private void RegisterHeroTemplates()
	{
		foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
		{
			if (type.IsAbstract || !typeof(HeroBase).IsAssignableFrom(type))
			{
				continue;
			}

			if (Activator.CreateInstance(type) is HeroBase hero)
			{
				AddChild(hero);
				AvailableHeroes.Add(hero);
			}
		}
	}

	private void OnStateChanged(GameState newState)
	{
		switch (newState)
		{
			case GameState.InMatch:
				break;
			case GameState.Result:
			case GameState.MainMenu:
			case GameState.HeroSelect:
				ResetHero();
				break;
		}
	}
}