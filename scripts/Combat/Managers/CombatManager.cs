using System;
using System.Collections.Generic;
using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Combat.Events;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Interfaces;

namespace Project_Star.Combat.Managers;

// 战斗管理器：统一计时与结算。每 0.2s 推进冷却与周期效果、发动主动能力，
// 结算效果队列并分发事件；被动能力按事件类型路由响应（连锁代数 ≤3 阻断）。
[GlobalClass]
public partial class CombatManager : Node
{
	// 结算帧间隔（秒）
	private const float TICK_INTERVAL = 0.2f;

	// 连锁最大代数（根效果 0 起，最多 3 层被动嵌套）
	private const int MAX_CHAIN = 3;

	// 濒死阈值（生命比例，首次跌破时分发 NearDeathEvent）
	private const float NEAR_DEATH_THRESHOLD = 0.25f;

	private readonly List<AriaAbilityHandle> _abilityHandles = new();
	private readonly List<AriaEffectHandle> _activeEffects = new();
	private readonly Dictionary<Type, List<ActivePassive>> _passiveIndex = new();
	private readonly Queue<EffectResolution> _resolutionQueue = new();
	private readonly HashSet<ICombatant> _belowHalf = new();
	private readonly HashSet<ICombatant> _nearDeath = new();

	private float _accumulator;
	private bool _inBattle;

	// 我方英雄
	public ICombatant? FriendlyHero { get; private set; }

	// 我方卡牌
	public List<ICombatant> FriendlyCards { get; } = new();

	// 敌方英雄
	public ICombatant? EnemyHero { get; private set; }

	// 敌方卡牌（列表顺序即棋盘布局顺序，用于相邻判定）
	public List<ICombatant> EnemyCards { get; } = new();

	public override void _PhysicsProcess(double delta)
	{
		if (!_inBattle)
		{
			return;
		}

		_accumulator += (float)delta;
		while (_accumulator >= TICK_INTERVAL)
		{
			_accumulator -= TICK_INTERVAL;
			TickFrame();
		}
	}

	// 开始战斗：登记参战双方、清空旧状态并分发 BattleStart 事件
	public void StartBattle(ICombatant? friendlyHero, IReadOnlyCollection<ICombatant> friendlyCards,
		ICombatant? enemyHero, IReadOnlyCollection<ICombatant> enemyCards)
	{
		ClearState();

		FriendlyHero = friendlyHero;
		FriendlyCards.AddRange(friendlyCards);
		EnemyHero = enemyHero;
		EnemyCards.AddRange(enemyCards);

		RegisterCombatant(friendlyHero);
		foreach (ICombatant card in friendlyCards)
		{
			RegisterCombatant(card);
		}
		RegisterCombatant(enemyHero);
		foreach (ICombatant card in enemyCards)
		{
			RegisterCombatant(card);
		}

		_inBattle = true;
		Dispatch(new BattleStartEvent(), null, BuildContext(null, null), 0);
	}

	// 结束战斗：清空所有战斗状态
	public void EndBattle()
	{
		ClearState();
		_inBattle = false;
	}

	// 清空战斗状态
	private void ClearState()
	{
		_abilityHandles.Clear();
		_activeEffects.Clear();
		_passiveIndex.Clear();
		_resolutionQueue.Clear();
		_belowHalf.Clear();
		_nearDeath.Clear();
		FriendlyCards.Clear();
		EnemyCards.Clear();
		FriendlyHero = null;
		EnemyHero = null;
		_accumulator = 0f;
	}

	// 登记单个参战实体：为每个能力建句柄，被动能力按响应事件类型入索引
	private void RegisterCombatant(ICombatant? combatant)
	{
		if (combatant is null)
		{
			return;
		}

		foreach (AriaAbilityBase ability in combatant.Abilities)
		{
			var handle = new AriaAbilityHandle(ability, combatant);
			_abilityHandles.Add(handle);

			if (ability is IPassiveAbility passive)
			{
				if (!_passiveIndex.TryGetValue(passive.ReactEventType, out List<ActivePassive>? list))
				{
					list = new List<ActivePassive>();
					_passiveIndex[passive.ReactEventType] = list;
				}

				list.Add(new ActivePassive(handle, combatant));
			}
		}
	}

	// 执行一帧：计时推进 → 发动主动能力 → 结算队列排水
	private void TickFrame()
	{
		AdvanceTimers();
		ActivateActiveAbilities();
		DrainQueue();
	}

	// 计时推进：冷却递减；active 效果计时，到期入队结算项、耗尽移除
	private void AdvanceTimers()
	{
		foreach (AriaAbilityHandle handle in _abilityHandles)
		{
			handle.UpdateCooldown(TICK_INTERVAL);
		}

		for (int i = _activeEffects.Count - 1; i >= 0; i--)
		{
			AriaEffectHandle active = _activeEffects[i];
			active.AdvanceTime(TICK_INTERVAL);

			if (active.IsTickDue)
			{
				ICombatant? owner = OwnerOf(active);
				ICombatant? target = TargetOf(active);
				foreach (AriaEffectBase tickEffect in active.Definition.GetTickEffects(BuildContext(owner, target)))
				{
					_resolutionQueue.Enqueue(new EffectResolution(tickEffect, owner, target, 0));
				}

				active.ResetTickTimer();
			}

			if (active.IsExpired)
			{
				active.Definition.Remove(BuildContext(OwnerOf(active), TargetOf(active)));
				_activeEffects.RemoveAt(i);
			}
		}
	}

	// 取句柄所属实体
	private static ICombatant? OwnerOf(AriaEffectHandle handle) => handle.Owner as ICombatant;

	// 取句柄目标实体
	private static ICombatant? TargetOf(AriaEffectHandle handle) => handle.Target as ICombatant;

	// 发动主动能力（非被动能力中冷却就绪的），并分发 AbilityActivated 事件
	private void ActivateActiveAbilities()
	{
		foreach (AriaAbilityHandle handle in _abilityHandles)
		{
			AriaAbilityBase ability = handle.Definition;
			if (ability is IPassiveAbility || !handle.IsReady)
			{
				continue;
			}

			ICombatant? owner = handle.Owner as ICombatant;
			BattleContext ctx = BuildContext(owner, null);
			if (!ability.CanActivate(ctx))
			{
				continue;
			}

			foreach (AriaAction action in ability.Activate(ctx))
			{
				EnqueueAction(action, owner, 0);
			}

			handle.StartCooldown();
			CombatEventBus.RaiseAbilityActivated(owner, ability);
			Dispatch(new AbilityActivatedEvent(owner, ability), handle, ctx, 0);

			if (owner is not null)
			{
				List<ICombatant> adjacent = GetAdjacentCards(owner);
				if (adjacent.Count > 0)
				{
					Dispatch(new AdjacentCardActivatedEvent(owner, adjacent), null, ctx, 0);
				}
			}
		}
	}

	// 取某卡牌的相邻卡牌：按所属方卡牌列表顺序（即棋盘布局顺序）取左右邻居
	private List<ICombatant> GetAdjacentCards(ICombatant card)
	{
		List<ICombatant> side = FriendlyCards.Contains(card) ? FriendlyCards : EnemyCards;
		int index = side.IndexOf(card);
		var adjacent = new List<ICombatant>(2);
		if (index < 0)
		{
			return adjacent;
		}

		if (index > 0)
		{
			adjacent.Add(side[index - 1]);
		}

		if (index + 1 < side.Count)
		{
			adjacent.Add(side[index + 1]);
		}

		return adjacent;
	}

	// 把一次动作的效果全部入队
	private void EnqueueAction(AriaAction action, ICombatant? source, int generation)
	{
		foreach ((IAriaEntity target, AriaEffectBase[] effects) in action.EffectsByTarget)
		{
			foreach (AriaEffectBase effect in effects)
			{
				_resolutionQueue.Enqueue(new EffectResolution(effect, source, target as ICombatant, generation));
			}
		}
	}

	// 结算队列排水：逐个 Apply 并分发结算/伤害事件，触发被动入队（连锁代数 ≤3）
	private void DrainQueue()
	{
		while (_resolutionQueue.Count > 0)
		{
			ResolveEffect(_resolutionQueue.Dequeue());
		}
	}

	// 结算单个效果：Apply → 注册持续/永久效果 → 分发结算/伤害事件 → 触发被动
	private void ResolveEffect(EffectResolution resolution)
	{
		BattleContext ctx = BuildContext(resolution.Source, resolution.Target);
		resolution.Effect.Apply(ctx);

		if (resolution.Effect.DurationType is AriaEffectDurationType.HasDuration or AriaEffectDurationType.Permanent)
		{
			_activeEffects.Add(new AriaEffectHandle(resolution.Effect, resolution.Source, resolution.Target));
		}

		CombatEventBus.RaiseEffectApplied(resolution.Target, resolution.Effect);
		Dispatch(new EffectAppliedEvent(resolution.Target, resolution.Effect), null, ctx, resolution.Generation);

		if (resolution.Effect is IDamageEffect damage)
		{
			var info = new DamageInfo
			{
				Source = resolution.Source,
				Target = resolution.Target!,
				Amount = damage.DamageAmount,
				IsPiercing = damage.IsPiercing,
				AbsorbedByArmor = damage.LastAbsorbedByArmor,
			};
			CombatEventBus.RaiseDamageDealt(info);
			Dispatch(new DamageDealtEvent(info), null, ctx, resolution.Generation);
			CheckHealthBelowHalf(resolution.Target);
			CheckNearDeath(resolution.Target);
		}
	}

	// 生命值首次跌破半血时分发事件
	private void CheckHealthBelowHalf(ICombatant? target)
	{
		if (target is null || _belowHalf.Contains(target))
		{
			return;
		}

		AriaAttributeSet? set = target.AttributeSet;
		AriaAttributeData? health = set?.GetAttribute(HeroAttributeSet.HEALTH);
		AriaAttributeData? maxHealth = set?.GetAttribute(HeroAttributeSet.MAX_HEALTH);
		if (health is null || maxHealth is null || health.CurrentValue > maxHealth.CurrentValue * 0.5f)
		{
			return;
		}

		_belowHalf.Add(target);
		CombatEventBus.RaiseHealthBelowHalf(target);
		Dispatch(new HealthBelowHalfEvent(target), null, BuildContext(null, target), 0);
	}

	// 生命值首次跌破濒死阈值时分发事件
	private void CheckNearDeath(ICombatant? target)
	{
		if (target is null || _nearDeath.Contains(target))
		{
			return;
		}

		AriaAttributeSet? set = target.AttributeSet;
		AriaAttributeData? health = set?.GetAttribute(HeroAttributeSet.HEALTH);
		AriaAttributeData? maxHealth = set?.GetAttribute(HeroAttributeSet.MAX_HEALTH);
		if (health is null || maxHealth is null || health.CurrentValue > maxHealth.CurrentValue * NEAR_DEATH_THRESHOLD)
		{
			return;
		}

		_nearDeath.Add(target);
		CombatEventBus.RaiseNearDeath(target);
		Dispatch(new NearDeathEvent(target), null, BuildContext(null, target), 0);
	}

	// 分发事件：按事件类型路由被动能力，产物代 +1，超过上限忽略
	private void Dispatch(CombatEventBase evt, AriaAbilityHandle? source, BattleContext ctx, int generation)
	{
		if (!_passiveIndex.TryGetValue(evt.GetType(), out List<ActivePassive>? passives))
		{
			return;
		}

		ctx.CurrentEvent = evt;
		foreach (ActivePassive passive in passives)
		{
			if (generation + 1 > MAX_CHAIN || ReferenceEquals(passive.Handle, source) || !passive.Handle.IsReady)
			{
				continue;
			}

			if (!passive.Handle.Definition.CanActivate(ctx))
			{
				continue;
			}

			CombatEventBus.RaiseAbilityActivated(passive.Combatant, passive.Handle.Definition);
			foreach (AriaAction action in passive.Handle.Definition.Activate(ctx))
			{
				EnqueueAction(action, passive.Combatant, generation + 1);
			}
		}
	}

	// 构造战斗上下文
	private BattleContext BuildContext(ICombatant? source, ICombatant? target)
	{
		var ctx = new BattleContext
		{
			Source = source,
			Target = target,
			FriendlyHero = FriendlyHero,
			EnemyHero = EnemyHero,
		};
		ctx.FriendlyCards.AddRange(FriendlyCards);
		ctx.EnemyCards.AddRange(EnemyCards);
		return ctx;
	}

	// 被动能力登记项
	private readonly struct ActivePassive
	{
		// 能力句柄
		public AriaAbilityHandle Handle { get; }

		// 所属实体
		public ICombatant Combatant { get; }

		public ActivePassive(AriaAbilityHandle handle, ICombatant combatant)
		{
			Handle = handle;
			Combatant = combatant;
		}
	}

	// 待结算效果项
	private readonly struct EffectResolution
	{
		// 效果
		public AriaEffectBase Effect { get; }

		// 来源
		public ICombatant? Source { get; }

		// 目标
		public ICombatant? Target { get; }

		// 连锁代数（根效果 0）
		public int Generation { get; }

		public EffectResolution(AriaEffectBase effect, ICombatant? source, ICombatant? target, int generation)
		{
			Effect = effect;
			Source = source;
			Target = target;
			Generation = generation;
		}
	}
}