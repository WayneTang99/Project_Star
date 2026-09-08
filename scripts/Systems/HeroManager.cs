using System;
using System.Reflection;
using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.Systems;

// 英雄管理器：反射收集英雄模板池，玩家选择后复制独立实例。
[GlobalClass]
public partial class HeroManager : Node
{
	private GameManager _gameManager = null!;

	// 可选英雄模板池
	public Godot.Collections.Array<HeroBase> AvailableHeroes { get; private set; } = new();

	// 当前选中的英雄实例；未选择时为 null
	public HeroBase? CurrentHero { get; private set; }

	// 英雄选中事件
	public event Action<HeroBase>? HeroSelectedEvent;

	// 英雄重置事件
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

	// 从模板复制英雄实例作为当前英雄，并通知状态机进入局内
	public void SelectHero(HeroBase template)
	{
		HeroBase instance = (HeroBase)template.Duplicate();
		instance.AttributeSet = (HeroAttributeSet)AttributeSetCopier.DeepCopy(template.AttributeSet);
		AddChild(instance);

		CurrentHero = instance;
		HeroSelectedEvent?.Invoke(instance);
		_gameManager.HeroSelected();
	}

	// 释放当前英雄实例并广播重置事件
	public void ResetHero()
	{
		if (CurrentHero is not null)
		{
			CurrentHero.QueueFree();
		}

		CurrentHero = null;
		HeroResetEvent?.Invoke();
	}

	// 反射收集所有非抽象公开的 HeroBase 子类作为模板（过滤内部测试类）
	private void RegisterHeroTemplates()
	{
		foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
		{
			if (type.IsAbstract || !type.IsPublic || !typeof(HeroBase).IsAssignableFrom(type))
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

	// 状态切换时重置英雄：离开局内即释放当前英雄
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