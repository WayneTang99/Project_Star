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
using Project_Star.Match.Events;

namespace Project_Star.Combat.Managers;

// 战斗管理器：统一计时与结算。每 0.2s 推进冷却与周期效果、发动主动能力、按属性结算 DoT，
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

	// 辐射每秒半衰因子（值 × 0.5^dt，每秒减半）
	private const float DOT_RADIATION_HALFLIFE = 0.5f;

	// 腐蚀每秒线性衰减量
	private const float DOT_CORROSION_DECAY_PER_SECOND = 1f;

	// 再生每秒线性衰减量
	private const float DOT_REGENERATION_DECAY_PER_SECOND = 1f;

	// DoT 结算下限（低于该值不再结算）
	private const float DOT_EPSILON = 0.01f;

	private readonly List<AriaAbilityHandle> _abilityHandles = new();
	private readonly List<AriaEffectHandle> _activeEffects = new();
	private readonly Dictionary<Type, List<ActivePassive>> _passiveIndex = new();
	private readonly Queue<EffectResolution> _resolutionQueue = new();
	private readonly HashSet<ICombatant> _belowHalf = new();
	private readonly HashSet<ICombatant> _nearDeath = new();
	private readonly Dictionary<int, float> _durationContributions = new();

	private float _accumulator;
	private bool _inBattle;

	// 是否处于战斗中
	public bool IsInBattle => _inBattle;

	// 最近一场战斗的胜者（战斗结束时记录，供 UI / 消费者读取）
	public ICombatant? LastWinner { get; private set; }

	// 当前战斗是否为 PvP
	public bool IsPvP { get; private set; }

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
			AdvanceTimers();
			TickFrame();
		}
	}

	// 开始战斗：登记参战双方、清空旧状态并分发 BattleStart 事件
	public void StartBattle(ICombatant? friendlyHero, IReadOnlyCollection<ICombatant> friendlyCards,
		ICombatant? enemyHero, IReadOnlyCollection<ICombatant> enemyCards, bool isPvP = false)
	{
		ClearState();

		FriendlyHero = friendlyHero;
		FriendlyCards.AddRange(friendlyCards);
		EnemyHero = enemyHero;
		EnemyCards.AddRange(enemyCards);
		IsPvP = isPvP;

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
		GD.Print($"[StartBattle] handles={_abilityHandles.Count} friendlyCards={FriendlyCards.Count} enemyCards={EnemyCards.Count}");
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
		_durationContributions.Clear();
		LastWinner = null;
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

	// 执行一帧：计时推进 → 发动主动能力 → DoT 结算 → 结算队列排水 → 死亡判定
	private void TickFrame()
	{
		_tickCount++;
		ActivateActiveAbilities();
		ApplyDot();
		DrainQueue();
		CheckBattleEnd();
	}

	private int _tickCount;

	// 计时推进：卡牌冷却递减；active 效果计时，到期入队结算项、耗尽移除；递减卡牌超频/麻痹时长
	private void AdvanceTimers()
	{
		// 递减卡牌冷却（每张卡只递减一次）
		var cooledDown = new HashSet<CardBase>();
		foreach (AriaAbilityHandle handle in _abilityHandles)
		{
			if (handle.Owner is CardBase card && cooledDown.Add(card))
			{
				float multiplier = GetCooldownMultiplier(handle);
				float current = card.AttributeSet.Cooldown.CurrentValue;
				float newVal = Mathf.Max(0f, current - TICK_INTERVAL * multiplier);
				if (Mathf.IsEqualApprox(newVal, 0f)) newVal = 0f;
				card.AttributeSet.Cooldown.SetCurrentValue(newVal);
			}
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
				// 到期扣减本实例贡献的时长（而非直接清零），支持多来源叠加
				if (_durationContributions.Remove(active.GetHashCode(), out float contributed))
				{
					SubtractCardDuration(active.Target, active.Definition, contributed);
				}

				_activeEffects.RemoveAt(i);
			}
		}

		TickCardDurations();
	}

	// 取句柄所属实体
	private static ICombatant? OwnerOf(AriaEffectHandle handle) => handle.Owner as ICombatant;

	// 取句柄目标实体
	private static ICombatant? TargetOf(AriaEffectHandle handle) => handle.Target as ICombatant;

	// 计算冷却速度倍率：超频 ×2、麻痹 ×0.5、同时存在时抵消（×1）
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

	// 递减所有卡牌的超频/麻痹时长属性（每帧 TICK_INTERVAL）
	private void TickCardDurations()
	{
		ICombatant[] allCards = FriendlyCards.Concat(EnemyCards).ToArray();
		foreach (ICombatant card in allCards)
		{
			if (card?.AttributeSet is not CardAttributeSet c) continue;
			if (c.OverclockDuration.CurrentValue > 0f)
				c.OverclockDuration.SetCurrentValue(Mathf.Max(0f, c.OverclockDuration.CurrentValue - TICK_INTERVAL));
			if (c.ParalysisDuration.CurrentValue > 0f)
				c.ParalysisDuration.SetCurrentValue(Mathf.Max(0f, c.ParalysisDuration.CurrentValue - TICK_INTERVAL));
		}
	}

	// 根据效果类型返回目标卡牌对应的时长属性
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

	// 获取效果施加的时长秒数
	private static float GetEffectDuration(AriaEffectBase effect) => effect switch
	{
		OverclockEffect oc => oc.Duration,
		ParalysisEffect pa => pa.Duration,
		_ => 0f,
	};

	// 到期扣减：从目标卡牌对应属性中减去本实例贡献的时长，下限 0
	private static void SubtractCardDuration(IAriaEntity? target, AriaEffectBase effect, float contributed)
	{
		AriaAttributeData? attr = GetCardDurationAttribute(target, effect);
		if (attr is null) return;
		attr.SetCurrentValue(Mathf.Max(0f, attr.CurrentValue - contributed));
	}

	// 读取某实体的剩余冷却（供 UI 展示）
	// 卡牌：返回卡牌级 Cooldown；英雄：返回能力级句柄冷却
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

	// 立即减少指定实体所有能力的冷却秒数
	public void ReduceCooldownFor(ICombatant combatant, float seconds)
	{
		foreach (AriaAbilityHandle handle in _abilityHandles)
		{
			if (ReferenceEquals(handle.Owner, combatant))
			{
				handle.ReduceCooldown(seconds);
			}
		}
	}

	// 发动主动能力（非被动能力中冷却就绪的），并分发 AbilityActivated 事件
	// 发动主动能力：按卡牌分组，卡牌冷却就绪时发动该卡牌所有主动能力
	private void ActivateActiveAbilities()
	{
		// 收集所有主动能力，按所属卡牌分组
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
		foreach ((CardBase card, List<AriaAbilityHandle> handles) in cardAbilities)
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
				catch (Exception ex)
				{
					GD.PrintErr($"[CombatManager] 能力发动异常: {ex.Message}\n{ex.StackTrace}");
				}
			}

			// 发动完毕，启动卡牌冷却
			card.AttributeSet.Cooldown.SetCurrentValue(card.CooldownDuration);
		}

		// 英雄：保留原有逻辑（无卡牌冷却，用能力自身冷却）
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
			CombatEventBus.RaiseAbilityActivated(owner, ability);
			Dispatch(new AbilityActivatedEvent(owner, ability), handle, ctx, 0);
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

	// 结算单个效果：Apply → 注册持续/永久效果（记录时长贡献） → 分发结算/伤害事件 → 触发被动
	private void ResolveEffect(EffectResolution resolution)
	{
		BattleContext ctx = BuildContext(resolution.Source, resolution.Target);
		resolution.Effect.Apply(ctx);

		if (resolution.Effect.DurationType is AriaEffectDurationType.HasDuration or AriaEffectDurationType.Permanent)
		{
			var handle = new AriaEffectHandle(resolution.Effect, resolution.Source, resolution.Target);
			_activeEffects.Add(handle);

			// 记录本次 Apply 写入的时长增量（用于到期扣减，支持多来源叠加）
			if (resolution.Effect is OverclockEffect or ParalysisEffect)
			{
				AriaAttributeData? attr = GetCardDurationAttribute(resolution.Target, resolution.Effect);
				if (attr is not null)
				{
					// Apply 已执行，属性值 = 旧值 + 本次增量；增量 = 属性当前值 - AriaEffectHandle 记录的 DurationSeconds
					// 但因为 TickCardDurations 可能已递减过，直接用属性变化量更准确：
					// 此处简化：记录该 handle 的 DurationSeconds 作为贡献（Apply 累加了该值）
					_durationContributions[handle.GetHashCode()] = GetEffectDuration(resolution.Effect);
				}
			}
		}

		CombatEventBus.RaiseEffectApplied(resolution.Target, resolution.Effect);
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

	// 治疗净化：削减目标辐射/腐蚀属性，削减总量 = 治疗量 × 50%，按当前值比例分摊
	private void PurgeDot(ICombatant? target, float healAmount)
	{
		if (target?.AttributeSet is not { } set)
		{
			return;
		}

		AriaAttributeData? radiation = set.GetAttribute(HeroAttributeSet.RADIATION);
		AriaAttributeData? corrosion = set.GetAttribute(HeroAttributeSet.CORROSION);
		if (radiation is null && corrosion is null)
		{
			return;
		}

		float rad = radiation?.CurrentValue ?? 0f;
		float corr = corrosion?.CurrentValue ?? 0f;
		float total = rad + corr;
		if (total <= 0f)
		{
			return;
		}

		float deduction = healAmount * 0.5f;
		if (radiation is not null)
		{
			radiation.SetCurrentValue(Mathf.Max(0f, rad - deduction * (rad / total)));
		}

		if (corrosion is not null)
		{
			corrosion.SetCurrentValue(Mathf.Max(0f, corr - deduction * (corr / total)));
		}
	}

	// DoT 结算：按辐射/腐蚀属性值结算每秒伤害并衰减，复用统一结算管线入队伤害效果
	private void ApplyDot()
	{
		ApplyDotTo(FriendlyHero);
		ApplyDotTo(EnemyHero);
		foreach (ICombatant card in FriendlyCards)
		{
			ApplyDotTo(card);
		}

		foreach (ICombatant card in EnemyCards)
		{
			ApplyDotTo(card);
		}
	}

	// 结算单个目标的 DoT/HoT：辐射穿透、腐蚀普通（值 ×dt 为每秒伤害）；再生直接恢复生命（值 ×dt）；随后衰减
	private void ApplyDotTo(ICombatant? combatant)
	{
		if (combatant?.AttributeSet is not { } set)
		{
			return;
		}

		AriaAttributeData? radiation = set.GetAttribute(HeroAttributeSet.RADIATION);
		AriaAttributeData? corrosion = set.GetAttribute(HeroAttributeSet.CORROSION);
		AriaAttributeData? regen = set.GetAttribute(HeroAttributeSet.REGENERATION);
		if (radiation is null && corrosion is null && regen is null)
		{
			return;
		}

		float dt = TICK_INTERVAL;
		if (radiation is not null && radiation.CurrentValue > DOT_EPSILON)
		{
			float rad = radiation.CurrentValue;
			_resolutionQueue.Enqueue(new EffectResolution(new PierceDamageEffect { DamageAmount = rad * dt }, null, combatant, 0));
			radiation.SetCurrentValue(rad * Mathf.Pow(DOT_RADIATION_HALFLIFE, dt));
		}

		if (corrosion is not null && corrosion.CurrentValue > DOT_EPSILON)
		{
			float corr = corrosion.CurrentValue;
			_resolutionQueue.Enqueue(new EffectResolution(new DamageEffect { DamageAmount = corr * dt }, null, combatant, 0));
			corrosion.SetCurrentValue(Mathf.Max(0f, corr - DOT_CORROSION_DECAY_PER_SECOND * dt));
		}

		if (regen is not null && regen.CurrentValue > DOT_EPSILON)
		{
			float reg = regen.CurrentValue;
			if (set.GetAttribute(HeroAttributeSet.HEALTH) is { } health)
			{
				health.SetCurrentValue(health.CurrentValue + reg * dt);
			}

			regen.SetCurrentValue(Mathf.Max(0f, reg - DOT_REGENERATION_DECAY_PER_SECOND * dt));
		}
	}

	// 判断实体是否死亡（生命值 ≤ 0）
	private static bool IsDead(ICombatant? combatant)
	{
		AriaAttributeData? health = combatant?.AttributeSet.GetAttribute(HeroAttributeSet.HEALTH);
		return health is not null && health.CurrentValue <= 0f;
	}

	// 战斗结局检测：任一方英雄生命归零即结束战斗并广播胜负事件（双方同时阵亡按己方胜）
	private void CheckBattleEnd()
	{
		bool friendlyDown = IsDead(FriendlyHero);
		bool enemyDown = IsDead(EnemyHero);
		if (!friendlyDown && !enemyDown)
		{
			return;
		}

		// 敌方阵亡则己方胜（含平局场景）；否则己方阵亡则敌方胜
		ICombatant? winner = enemyDown ? FriendlyHero : EnemyHero;
		if (winner is null)
		{
			return;
		}

		// 计算敌方剩余生命比例（0~1），供奖励按血量缩放
		float enemyRatio = 0f;
		if (EnemyHero is not null && EnemyHero.AttributeSet is not null)
		{
			var health = EnemyHero.AttributeSet.GetAttribute("Health");
			var maxHealth = EnemyHero.AttributeSet.GetAttribute("MaxHealth");
			if (health is not null && maxHealth is not null && maxHealth.CurrentValue > 0f)
			{
				enemyRatio = Mathf.Clamp(health.CurrentValue / maxHealth.CurrentValue, 0f, 1f);
			}
		}

		bool isPvP = IsPvP;
		EndBattle();
		LastWinner = winner;
		MatchEventBus.Raise(new BattleWonEvent(winner, enemyRatio, isPvP));
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

			ctx.Self = passive.Combatant;
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

	// 构造战斗上下文（根据 source 归属自动翻转敌我视角）
	private BattleContext BuildContext(ICombatant? source, ICombatant? target)
	{
		bool isEnemy = source is not null && IsEnemy(source);

		var ctx = new BattleContext
		{
			Source = source,
			Target = target,
			Self = source,
			FriendlyHero = isEnemy ? EnemyHero : FriendlyHero,
			EnemyHero = isEnemy ? FriendlyHero : EnemyHero,
		};

		if (isEnemy)
		{
			ctx.FriendlyCards.AddRange(EnemyCards);
			ctx.EnemyCards.AddRange(FriendlyCards);
		}
		else
		{
			ctx.FriendlyCards.AddRange(FriendlyCards);
			ctx.EnemyCards.AddRange(EnemyCards);
		}

		return ctx;
	}

	// 判断实体是否属于敌方
	private bool IsEnemy(ICombatant combatant)
	{
		if (ReferenceEquals(combatant, EnemyHero)) return true;
		foreach (ICombatant c in EnemyCards)
		{
			if (ReferenceEquals(c, combatant)) return true;
		}

		return false;
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