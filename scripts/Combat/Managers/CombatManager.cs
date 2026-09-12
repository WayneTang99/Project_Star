using System;
using System.Collections.Generic;
using Aria;
using Godot;
using Project_Star.Core.Interfaces;
using Project_Star.Match.Events;

namespace Project_Star.Combat.Managers;

// 战斗管理器（Node）：持有 BattleClock + Resolver，管理战斗生命周期与 A/B 超时。
// 每步长（1/30s）调用 Resolver.Tick，超时自动结束战斗。
[GlobalClass]
public partial class CombatManager : Node
{
	// A 超时（秒）：正常战斗上限
	private const float TIMEOUT_A = 300f;

	// B 超时（秒）：延长上限（如 PvP 镜像战斗）
	private const float TIMEOUT_B = 600f;

	private BattleClock _clock = new();
	private Resolver _resolver = new();

	// 快照历史（用于回滚，最多保留最近 N 步）
	private const int MAX_SNAPSHOT_HISTORY = 300;
	private readonly List<BattleSnapshot> _snapshots = new();

	private bool _inBattle;
	private float _elapsed;

	// 是否处于战斗中
	public bool IsInBattle => _inBattle;

	// 最近一场战斗的胜者
	public ICombatant? LastWinner { get; private set; }

	// 当前战斗是否为 PvP
	public bool IsPvP { get; private set; }

	// 使用 B 超时（PvP 时使用更长超时）
	private bool _useTimeoutB;

	public override void _PhysicsProcess(double delta)
	{
		if (!_inBattle) return;

		_elapsed += (float)delta;

		// A/B 超时检测
		float timeout = _useTimeoutB ? TIMEOUT_B : TIMEOUT_A;
		if (_elapsed >= timeout)
		{
			GD.Print($"[CombatManager] 战斗超时 ({timeout}s)，强制结束");
			ForceEnd();
			return;
		}

		_clock.Advance((float)delta);
	}

	// 开始战斗
	public void StartBattle(ICombatant? friendlyHero, IReadOnlyCollection<ICombatant> friendlyCards,
		ICombatant? enemyHero, IReadOnlyCollection<ICombatant> enemyCards, bool isPvP = false)
	{
		_resolver.Clear();
		_snapshots.Clear();
		_clock.Reset();
		_elapsed = 0f;

		IsPvP = isPvP;
		_useTimeoutB = isPvP;
		LastWinner = null;
		_inBattle = true;

		_resolver.Setup(friendlyHero, friendlyCards, enemyHero, enemyCards);

		// 监听时钟步进
		_clock.StepTicked += OnClockTick;

		GD.Print($"[StartBattle] PvP={isPvP} friendlyCards={friendlyCards.Count} enemyCards={enemyCards.Count}");
	}

	// 结束战斗
	public void EndBattle()
	{
		_inBattle = false;
		_clock.StepTicked -= OnClockTick;
		_resolver.Clear();
	}

	// 强制结束战斗（超时）
	private void ForceEnd()
	{
		ICombatant? winner = _resolver.GetWinner();
		if (winner is null)
		{
			// 超时无胜者，判定为己方失败
			winner = _resolver.GetEnemyHero();
		}

		float enemyRatio = GetEnemyRemainingRatio();
		bool isPvP = IsPvP;
		EndBattle();
		LastWinner = winner;
		MatchEventBus.Raise(new BattleWonEvent(winner!, enemyRatio, isPvP));
	}

	// 每步回调：执行 Resolver.Tick → 快照 → 死亡判定
	private void OnClockTick()
	{
		if (!_inBattle) return;

		_resolver.Tick(BattleClock.FIXED_STEP);

		// 每步保存快照（限制数量）
		if (_snapshots.Count >= MAX_SNAPSHOT_HISTORY)
		{
			_snapshots.RemoveAt(0);
		}

		var snapshot = new BattleSnapshot(_resolver.StepCount);
		snapshot.Capture(_resolver.GetFriendlyHero());
		snapshot.Capture(_resolver.GetEnemyHero());
		foreach (ICombatant c in _resolver.GetFriendlyCards()) snapshot.Capture(c);
		foreach (ICombatant c in _resolver.GetEnemyCards()) snapshot.Capture(c);
		_snapshots.Add(snapshot);

		// 死亡判定
		if (_resolver.IsAnyHeroDead())
		{
			ICombatant? winner = _resolver.GetWinner();
			if (winner is not null)
			{
				float enemyRatio = GetEnemyRemainingRatio();
				bool isPvP = IsPvP;
				EndBattle();
				LastWinner = winner;
				MatchEventBus.Raise(new BattleWonEvent(winner, enemyRatio, isPvP));
			}
		}
	}

	// 计算敌方剩余生命比例
	private float GetEnemyRemainingRatio()
	{
		ICombatant? enemy = _resolver.GetEnemyHero();
		if (enemy?.AttributeSet is null) return 0f;

		var health = enemy.AttributeSet.GetAttribute("Health");
		var maxHealth = enemy.AttributeSet.GetAttribute("MaxHealth");
		if (health is null || maxHealth is null || maxHealth.CurrentValue <= 0f) return 0f;

		return Mathf.Clamp(health.CurrentValue / maxHealth.CurrentValue, 0f, 1f);
	}

	// 回滚到最近 N 步前的快照
	public bool Rollback(int stepsBack)
	{
		int targetIndex = _snapshots.Count - 1 - stepsBack;
		if (targetIndex < 0 || targetIndex >= _snapshots.Count)
		{
			return false;
		}

		_snapshots[targetIndex].RestoreAll();
		// 移除目标快照之后的所有快照
		_snapshots.RemoveRange(targetIndex + 1, _snapshots.Count - targetIndex - 1);
		return true;
	}

	// 获取剩余冷却（委托给 Resolver）
	public float GetCooldownRemaining(ICombatant combatant, AriaAbilityBase ability)
	{
		return _resolver.GetCooldownRemaining(combatant, ability);
	}
}
