using System;
using System.Collections.Generic;
using System.Linq;
using Aria;
using Godot;
using Project_Star.Combat.Contexts;
using Project_Star.Combat.Events;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Interfaces;
using Project_Star.Entities.Effects;

namespace Project_Star.Combat;

// 确定性战斗解析器（普通类）：按固定步长推进战斗状态。
// 包含冷却递减、主动能力发动、DoT 结算、效果队列排水、死亡判定。
public class Resolver
{
	// 连锁最大代数（根效果 0 起，最多 3 层被动嵌套）
	private const int MAX_CHAIN = 3;

	// 濒死阈值（生命比例，首次跌破时分发 NearDeathEvent）
	private const float NEAR_DEATH_THRESHOLD = 0.25f;

	// 辐射每秒半衰因子
	private const float DOT_RADIATION_HALFLIFE = 0.5f;

	// 腐蚀每秒线性衰减量
	private const float DOT_CORROSION_DECAY_PER_SECOND = 1f;

	// 再生每秒线性衰减量
	private const float DOT_REGENERATION_DECAY_PER_SECOND = 1f;

	// DoT 结算下限
	private const float DOT_EPSILON = 0.01f;

	private readonly List<AriaAbilityHandle> _abilityHandles = new();
	private readonly List<AriaEffectHandle> _activeEffects = new();
	private readonly Dictionary<Type, List<ActivePassive>> _passiveIndex = new();
	private readonly Queue<EffectResolution> _resolutionQueue = new();
	private readonly HashSet<ICombatant> _belowHalf = new();
	private readonly HashSet<ICombatant> _nearDeath = new();
	private readonly Dictionary<int, float> _durationContributions = new();

	// 战斗双方引用
	private ICombatant? _friendlyHero;
	private ICombatant? _enemyHero;
	private List<ICombatant> _friendlyCards = new();
	private List<ICombatant> _enemyCards = new();

	// 步数计数
	public int StepCount { get; private set; }

	// 是否有参战实体
	public bool HasCombatants => _abilityHandles.Count > 0;

	public Resolver()
	{
	}

	// 设置战斗双方
	public void Setup(
		ICombatant? friendlyHero, IReadOnlyCollection<ICombatant> friendlyCards,
		ICombatant? enemyHero, IReadOnlyCollection<ICombatant> enemyCards)
	{
		_friendlyHero = friendlyHero;
		_enemyHero = enemyHero;
		_friendlyCards = new List<ICombatant>(friendlyCards);
		_enemyCards = new List<ICombatant>(enemyCards);

		foreach (ICombatant c in _friendlyCards)
		{
			RegisterCombatant(c);
		}

		foreach (ICombatant c in _enemyCards)
		{
			RegisterCombatant(c);
		}

		RegisterCombatant(friendlyHero);
		RegisterCombatant(enemyHero);
	}

	// 清空所有战斗状态
	public void Clear()
	{
		_abilityHandles.Clear();
		_activeEffects.Clear();
		_passiveIndex.Clear();
		_resolutionQueue.Clear();
		_belowHalf.Clear();
		_nearDeath.Clear();
		_durationContributions.Clear();
		_friendlyHero = null;
		_enemyHero = null;
		_friendlyCards.Clear();
		_enemyCards.Clear();
		StepCount = 0;
	}

	// 执行一步（1/30秒）：计时推进 → 发动主动能力 → DoT → 结算队列 → 死亡判定
	public void Tick(float stepDelta)
	{
		StepCount++;
		AdvanceTimers(stepDelta);
		ActivateActiveAbilities(stepDelta);
		ApplyDot(stepDelta);
		DrainQueue();
	}

	// 是否有英雄死亡
	public bool IsAnyHeroDead()
	{
		return IsDead(_friendlyHero) || IsDead(_enemyHero);
	}

	// 获取胜者
	public ICombatant? GetWinner()
	{
		bool enemyDown = IsDead(_enemyHero);
		return enemyDown ? _friendlyHero : _enemyHero;
	}

	// 获取我方英雄
	public ICombatant? GetFriendlyHero() => _friendlyHero;

	// 获取敌方英雄
	public ICombatant? GetEnemyHero() => _enemyHero;

	// 获取我方卡牌
	public IReadOnlyList<ICombatant> GetFriendlyCards() => _friendlyCards;

	// 获取敌方卡牌
	public IReadOnlyList<ICombatant> GetEnemyCards() => _enemyCards;

	// 登记单个参战实体
	private void RegisterCombatant(ICombatant? combatant)
	{
		if (combatant is null)
		{
			return;
		}

		foreach (AriaAbilityBase ability in combatant.Abilities)
		{
			ability.Owner = combatant;
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

	// 计时推进：冷却递减、效果计时、时长属性递减
	private void AdvanceTimers(float stepDelta)
	{
		// 递减卡牌冷却
		var cooledDown = new HashSet<CardBase>();
		foreach (AriaAbilityHandle handle in _abilityHandles)
		{
			if (handle.Owner is CardBase card && cooledDown.Add(card))
			{
				float multiplier = GetCooldownMultiplier(handle);
				float current = card.AttributeSet.Cooldown.CurrentValue;
				float newVal = Mathf.Max(0f, current - stepDelta * multiplier);
				if (Mathf.IsEqualApprox(newVal, 0f)) newVal = 0f;
				card.AttributeSet.Cooldown.SetCurrentValue(newVal);
			}
		}

		// 效果计时
		for (int i = _activeEffects.Count - 1; i >= 0; i--)
		{
			AriaEffectHandle active = _activeEffects[i];
			active.AdvanceTime(stepDelta);

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
				if (_durationContributions.Remove(active.GetHashCode(), out float contributed))
				{
					SubtractCardDuration(active.Target, active.Definition, contributed);
				}

				_activeEffects.RemoveAt(i);
			}
		}

		// 递减卡牌超频/麻痹时长
		TickCardDurations(stepDelta);
	}

	// 发动主动能力
	private void ActivateActiveAbilities(float stepDelta)
	{
		var cardAbilities = new Dictionary<CardBase, List<AriaAbilityHandle>>();
		var heroAbilities = new List<AriaAbilityHandle>();

		foreach (AriaAbilityHandle handle in _abilityHandles)
		{
			AriaAbilityBase ability = handle.Definition;
			if (ability is IPassiveAbility) continue;

			if (handle.Owner is CardBase card)
			{
				if (!cardAbilities.TryGetValue(card, out List<AriaAbilityHandle>? list))
				{
					list = new List<AriaAbilityHandle>();
					cardAbilities[card] = list;
				}
				list.Add(handle);
			}
			else
			{
				heroAbilities.Add(handle);
			}
		}

		// 卡牌：冷却就绪时发动所有主动能力
		foreach (var (card, handles) in cardAbilities)
		{
			if (card.AttributeSet.Cooldown.CurrentValue > 0f) continue;

			foreach (AriaAbilityHandle handle in handles)
			{
				try
				{
					AriaAbilityBase ability = handle.Definition;
					ICombatant? owner = handle.Owner as ICombatant;
					BattleContext ctx = BuildContext(owner, null);
					if (!ability.CanActivate(ctx)) continue;

					AriaAction[] actions = ability.Activate(ctx);
					foreach (AriaAction action in actions)
					{
						EnqueueAction(action, owner, 0);
					}

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
				catch (Exception ex)
				{
					GD.PrintErr($"[Resolver] 能力发动异常: {ex.Message}\n{ex.StackTrace}");
				}
			}

			card.AttributeSet.Cooldown.SetCurrentValue(card.CooldownDuration);
		}

		// 英雄：用能力自身冷却
		foreach (AriaAbilityHandle handle in heroAbilities)
		{
			AriaAbilityBase ability = handle.Definition;
			if (!handle.IsReady) continue;

			ICombatant? owner = handle.Owner as ICombatant;
			BattleContext ctx = BuildContext(owner, null);
			if (!ability.CanActivate(ctx)) continue;

			foreach (AriaAction action in ability.Activate(ctx))
			{
				EnqueueAction(action, owner, 0);
			}

			handle.StartCooldown();
			Dispatch(new AbilityActivatedEvent(owner, ability), handle, ctx, 0);
		}
	}

	// 取某卡牌的相邻卡牌
	private List<ICombatant> GetAdjacentCards(ICombatant card)
	{
		List<ICombatant> side = _friendlyCards.Contains(card) ? _friendlyCards : _enemyCards;
		int index = side.IndexOf(card);
		var adjacent = new List<ICombatant>(2);
		if (index < 0) return adjacent;

		if (index > 0) adjacent.Add(side[index - 1]);
		if (index + 1 < side.Count) adjacent.Add(side[index + 1]);
		return adjacent;
	}

	// 把一次动作的效果全部入队
	private void EnqueueAction(AriaAction action, ICombatant? source, int generation)
	{
		foreach (var (target, effects) in action.EffectsByTarget)
		{
			foreach (AriaEffectBase effect in effects)
			{
				_resolutionQueue.Enqueue(new EffectResolution(effect, source, target as ICombatant, generation));
			}
		}
	}

	// 结算队列排水
	private void DrainQueue()
	{
		while (_resolutionQueue.Count > 0)
		{
			ResolveEffect(_resolutionQueue.Dequeue());
		}
	}

	// 结算单个效果
	private void ResolveEffect(EffectResolution resolution)
	{
		BattleContext ctx = BuildContext(resolution.Source, resolution.Target);
		resolution.Effect.Apply(ctx);

		if (resolution.Effect.DurationType is AriaEffectDurationType.HasDuration or AriaEffectDurationType.Permanent)
		{
			var handle = new AriaEffectHandle(resolution.Effect, resolution.Source, resolution.Target);
			_activeEffects.Add(handle);

			if (resolution.Effect is OverclockEffect or ParalysisEffect)
			{
				AriaAttributeData? attr = GetCardDurationAttribute(resolution.Target, resolution.Effect);
				if (attr is not null)
				{
					_durationContributions[handle.GetHashCode()] = GetEffectDuration(resolution.Effect);
				}
			}
		}

		Dispatch(new EffectAppliedEvent(resolution.Target, resolution.Effect), null, ctx, resolution.Generation);

		if (resolution.Effect is IHealEffect heal)
		{
			PurgeDot(resolution.Target, heal.HealAmount);
		}

		if (resolution.Effect is CooldownReductionEffect charge)
		{
			ICombatant? target = resolution.Target ?? resolution.Source;
			if (target is not null)
			{
				ReduceCooldownFor(target, charge.ReductionSeconds);
			}
		}

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

	// 治疗净化
	private void PurgeDot(ICombatant? target, float healAmount)
	{
		if (target?.AttributeSet is not { } set) return;

		AriaAttributeData? radiation = set.GetAttribute(HeroAttributeSet.RADIATION);
		AriaAttributeData? corrosion = set.GetAttribute(HeroAttributeSet.CORROSION);
		if (radiation is null && corrosion is null) return;

		float rad = radiation?.CurrentValue ?? 0f;
		float corr = corrosion?.CurrentValue ?? 0f;
		float total = rad + corr;
		if (total <= 0f) return;

		float deduction = healAmount * 0.5f;
		if (radiation is not null)
			radiation.SetCurrentValue(Mathf.Max(0f, rad - deduction * (rad / total)));
		if (corrosion is not null)
			corrosion.SetCurrentValue(Mathf.Max(0f, corr - deduction * (corr / total)));
	}

	// DoT 结算
	private void ApplyDot(float stepDelta)
	{
		ApplyDotTo(_friendlyHero, stepDelta);
		ApplyDotTo(_enemyHero, stepDelta);
		foreach (ICombatant card in _friendlyCards) ApplyDotTo(card, stepDelta);
		foreach (ICombatant card in _enemyCards) ApplyDotTo(card, stepDelta);
	}

	// 结算单个目标的 DoT/HoT
	private void ApplyDotTo(ICombatant? combatant, float stepDelta)
	{
		if (combatant?.AttributeSet is not { } set) return;

		AriaAttributeData? radiation = set.GetAttribute(HeroAttributeSet.RADIATION);
		AriaAttributeData? corrosion = set.GetAttribute(HeroAttributeSet.CORROSION);
		AriaAttributeData? regen = set.GetAttribute(HeroAttributeSet.REGENERATION);
		if (radiation is null && corrosion is null && regen is null) return;

		if (radiation is not null && radiation.CurrentValue > DOT_EPSILON)
		{
			float rad = radiation.CurrentValue;
			_resolutionQueue.Enqueue(new EffectResolution(new PierceDamageEffect { DamageAmount = rad * stepDelta }, null, combatant, 0));
			radiation.SetCurrentValue(rad * Mathf.Pow(DOT_RADIATION_HALFLIFE, stepDelta));
		}

		if (corrosion is not null && corrosion.CurrentValue > DOT_EPSILON)
		{
			float corr = corrosion.CurrentValue;
			_resolutionQueue.Enqueue(new EffectResolution(new DamageEffect { DamageAmount = corr * stepDelta }, null, combatant, 0));
			corrosion.SetCurrentValue(Mathf.Max(0f, corr - DOT_CORROSION_DECAY_PER_SECOND * stepDelta));
		}

		if (regen is not null && regen.CurrentValue > DOT_EPSILON)
		{
			float reg = regen.CurrentValue;
			if (set.GetAttribute(HeroAttributeSet.HEALTH) is { } health)
			{
				health.SetCurrentValue(health.CurrentValue + reg * stepDelta);
			}
			regen.SetCurrentValue(Mathf.Max(0f, reg - DOT_REGENERATION_DECAY_PER_SECOND * stepDelta));
		}
	}

	// 判断实体是否死亡
	private static bool IsDead(ICombatant? combatant)
	{
		AriaAttributeData? health = combatant?.AttributeSet.GetAttribute(HeroAttributeSet.HEALTH);
		return health is not null && health.CurrentValue <= 0f;
	}

	// 生命值首次跌破半血
	private void CheckHealthBelowHalf(ICombatant? target)
	{
		if (target is null || _belowHalf.Contains(target)) return;

		AriaAttributeSet? set = target.AttributeSet;
		AriaAttributeData? health = set?.GetAttribute(HeroAttributeSet.HEALTH);
		AriaAttributeData? maxHealth = set?.GetAttribute(HeroAttributeSet.MAX_HEALTH);
		if (health is null || maxHealth is null || health.CurrentValue > maxHealth.CurrentValue * 0.5f) return;

		_belowHalf.Add(target);
		CombatEventBus.RaiseHealthBelowHalf(target);
		Dispatch(new HealthBelowHalfEvent(target), null, BuildContext(null, target), 0);
	}

	// 生命值首次跌破濒死阈值
	private void CheckNearDeath(ICombatant? target)
	{
		if (target is null || _nearDeath.Contains(target)) return;

		AriaAttributeSet? set = target.AttributeSet;
		AriaAttributeData? health = set?.GetAttribute(HeroAttributeSet.HEALTH);
		AriaAttributeData? maxHealth = set?.GetAttribute(HeroAttributeSet.MAX_HEALTH);
		if (health is null || maxHealth is null || health.CurrentValue > maxHealth.CurrentValue * NEAR_DEATH_THRESHOLD) return;

		_nearDeath.Add(target);
		CombatEventBus.RaiseNearDeath(target);
		Dispatch(new NearDeathEvent(target), null, BuildContext(null, target), 0);
	}

	// 分发事件：按事件类型路由被动能力
	private void Dispatch(CombatEventBase evt, AriaAbilityHandle? source, BattleContext ctx, int generation)
	{
		if (!_passiveIndex.TryGetValue(evt.GetType(), out List<ActivePassive>? passives)) return;

		ctx.CurrentEvent = evt;
		foreach (ActivePassive passive in passives)
		{
			if (generation + 1 > MAX_CHAIN || ReferenceEquals(passive.Handle, source) || !passive.Handle.IsReady) continue;

			ctx.Self = passive.Combatant;
			if (!passive.Handle.Definition.CanActivate(ctx)) continue;

			foreach (AriaAction action in passive.Handle.Definition.Activate(ctx))
			{
				EnqueueAction(action, passive.Combatant, generation + 1);
			}
		}
	}

	// 构造战斗上下文
	private BattleContext BuildContext(ICombatant? source, ICombatant? target)
	{
		bool isEnemy = source is not null && IsEnemy(source);

		var ctx = new BattleContext
		{
			Source = source,
			Target = target,
			Self = source,
			FriendlyHero = isEnemy ? _enemyHero : _friendlyHero,
			EnemyHero = isEnemy ? _friendlyHero : _enemyHero,
		};

		if (isEnemy)
		{
			ctx.FriendlyCards.AddRange(_enemyCards);
			ctx.EnemyCards.AddRange(_friendlyCards);
		}
		else
		{
			ctx.FriendlyCards.AddRange(_friendlyCards);
			ctx.EnemyCards.AddRange(_enemyCards);
		}

		return ctx;
	}

	// 判断实体是否属于敌方
	private bool IsEnemy(ICombatant combatant)
	{
		if (ReferenceEquals(combatant, _enemyHero)) return true;
		foreach (ICombatant c in _enemyCards)
		{
			if (ReferenceEquals(c, combatant)) return true;
		}
		return false;
	}

	// 取句柄所属实体
	private static ICombatant? OwnerOf(AriaEffectHandle handle) => handle.Owner as ICombatant;

	// 取句柄目标实体
	private static ICombatant? TargetOf(AriaEffectHandle handle) => handle.Target as ICombatant;

	// 计算冷却速度倍率
	private static float GetCooldownMultiplier(AriaAbilityHandle handle)
	{
		if (handle.Owner?.AttributeSet is not CardAttributeSet card) return 1f;
		float oc = card.OverclockDuration.CurrentValue;
		float pa = card.ParalysisDuration.CurrentValue;
		if (oc > 0f && pa > 0f) return 1f;
		if (oc > 0f) return 2f;
		if (pa > 0f) return 0.5f;
		return 1f;
	}

	// 递减所有卡牌的超频/麻痹时长
	private void TickCardDurations(float stepDelta)
	{
		ICombatant[] allCards = _friendlyCards.Concat(_enemyCards).ToArray();
		foreach (ICombatant card in allCards)
		{
			if (card?.AttributeSet is not CardAttributeSet c) continue;
			if (c.OverclockDuration.CurrentValue > 0f)
				c.OverclockDuration.SetCurrentValue(Mathf.Max(0f, c.OverclockDuration.CurrentValue - stepDelta));
			if (c.ParalysisDuration.CurrentValue > 0f)
				c.ParalysisDuration.SetCurrentValue(Mathf.Max(0f, c.ParalysisDuration.CurrentValue - stepDelta));
		}
	}

	// 获取效果时长
	private static float GetEffectDuration(AriaEffectBase effect) => effect switch
	{
		OverclockEffect oc => oc.Duration,
		ParalysisEffect pa => pa.Duration,
		_ => 0f,
	};

	// 获取卡牌时长属性
	private static AriaAttributeData? GetCardDurationAttribute(IAriaEntity? target, AriaEffectBase effect)
	{
		if (target?.AttributeSet is not CardAttributeSet card) return null;
		return effect switch
		{
			OverclockEffect => card.OverclockDuration,
			ParalysisEffect => card.ParalysisDuration,
			_ => null,
		};
	}

	// 到期扣减时长
	private static void SubtractCardDuration(IAriaEntity? target, AriaEffectBase effect, float contributed)
	{
		AriaAttributeData? attr = GetCardDurationAttribute(target, effect);
		if (attr is null) return;
		attr.SetCurrentValue(Mathf.Max(0f, attr.CurrentValue - contributed));
	}

	// 立即减少指定实体所有能力的冷却
	private void ReduceCooldownFor(ICombatant combatant, float seconds)
	{
		foreach (AriaAbilityHandle handle in _abilityHandles)
		{
			if (ReferenceEquals(handle.Owner, combatant))
			{
				handle.ReduceCooldown(seconds);
			}
		}
	}

	// 获取剩余冷却（供外部查询）
	public float GetCooldownRemaining(ICombatant combatant, AriaAbilityBase ability)
	{
		if (combatant is CardBase card)
		{
			return card.AttributeSet.Cooldown.CurrentValue;
		}

		foreach (AriaAbilityHandle handle in _abilityHandles)
		{
			if (ReferenceEquals(handle.Owner, combatant) && ReferenceEquals(handle.Definition, ability))
			{
				return handle.CooldownRemaining;
			}
		}

		return 0f;
	}

	// 被动能力登记项
	private readonly struct ActivePassive
	{
		public AriaAbilityHandle Handle { get; }
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
		public AriaEffectBase Effect { get; }
		public ICombatant? Source { get; }
		public ICombatant? Target { get; }
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
