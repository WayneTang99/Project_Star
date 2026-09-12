using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Pools;
using Project_Star.Core.Types;

namespace Project_Star.Systems;

// 英雄管理器：通过 PoolBase 管理英雄模板池，玩家选择后复制独立实例。
[GlobalClass]
public partial class HeroManager : GlobalManagerBase
{
	private GameManager _gameManager = null!;
	private PoolBase<HeroBase> _pool = null!;

	// 可选英雄模板池
	public Godot.Collections.Array<HeroBase> AvailableHeroes { get; private set; } = new();

	// 当前选中的英雄实例；未选择时为 null
	public HeroBase? CurrentHero { get; private set; }

	// 英雄选中事件
	public event System.Action<HeroBase>? HeroSelectedEvent;

	// 英雄重置事件
	public event System.Action? HeroResetEvent;

	protected override void OnInitialize()
	{
		base.OnInitialize();
		_gameManager = GetNode<GameManager>("../GameManager");
		_gameManager.StateChangedEvent += OnStateChanged;

		_pool = new PoolBase<HeroBase>(this);
		_pool.RegisterTemplates();

		// 同步到公开属性供 UI 等外部读取
		foreach (HeroBase template in _pool.Templates)
		{
			AvailableHeroes.Add(template);
		}
	}

	protected override void OnShutdown()
	{
		base.OnShutdown();
		if (_gameManager is not null)
		{
			_gameManager.StateChangedEvent -= OnStateChanged;
		}
	}

	// 从模板复制英雄实例作为当前英雄，并通知状态机进入局内
	public void SelectHero(HeroBase template)
	{
		HeroBase instance = _pool.CreateInstance(template);
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
