# 能力与战斗框架设计

本文档记录能力（主动技能/被动技能）与战斗相关框架的设计与实现约定。

## 核心模型：能力触发效果

- **能力（Ability）**：可被触发的动作单元。主动（冷却自动）与被动（事件触发）都是能力；被动能力实现游戏侧接口 `IPassiveAbility` 声明响应的事件类型。能力类只负责**执行逻辑**。
- **效果（Effect）**：被能力触发的被动后果（静态修饰 / 周期 DoT-HoT / 即时 / 永久）。
- 触发、冷却、编排、连锁（≤3 层代数上限）统一由 `CombatManager` 负责；管理器只与实体（`ICombatant`）通信，不直接驱动能力。

## 结算模型：统一效果队列

一切伤害/效果最终都变成**效果结算项**进入同一个队列，由 CombatManager 的**唯一结算器**逐个 `Apply` 并统一分发事件。**事件分发永远在结算之后**，效果保持纯逻辑（只改属性，不引用总线/管理器）。

### 每 0.2s 帧流程（`_PhysicsProcess` 累积触发）

```
1. 计时推进
   - 所有能力句柄 UpdateCooldown(0.2)
   - 所有 active AriaEffectHandle AdvanceTime(0.2)：到期 → GetTickEffects() 产物以 gen0 入队；耗尽 → Remove + 清除
2. 发动主动能力（非被动能力、就绪、CanActivate）
   - Activate → 效果以 gen0 入队 → StartCooldown
   - 分发 AbilityActivatedEvent → 匹配被动 Activate → 效果以 gen1 入队
3. 结算队列排水（while 队列非空）
   - 出队 → Apply → 分发 EffectAppliedEvent / DamageDealtEvent / HealthBelowHalfEvent
   - 分发 → 匹配被动 Activate → 产物 gen+1 入队（gen+1 > MAX_CHAIN 则忽略）
4. 帧结束
```

- **连锁代数（generation）**：根效果 = 0，被动反应产物 = 触发它的效果代数 +1，超过 `MAX_CHAIN(3)` 不再产生。只限嵌套深度、不限宽度，独立级联互不占预算。
- **类型检测**：被动按 `IPassiveAbility.ReactEventType`（`CombatEventBase` 子类类型）入索引，分发按 `evt.GetType()` 路由，不匹配不响应；同句柄不响应自身分发。
- **周期效果**：通过 `AriaEffectBase.GetTickEffects()` 入队统一结算（如辐射/腐蚀返回内嵌即时效果），与能力效果同路径，自动触发伤害事件。

## 分层

### Aria 层（`addons/aria/`，游戏无关，保留 `Aria` 前缀）

| 文件 | 说明 |
|---|---|
| `interfaces/IAriaEntity.cs` | 最小实体接口：暴露 `AriaAttributeSet AttributeSet` |
| `contexts/AriaContextBase.cs` | 上下文基类，供游戏侧继承自定义 |
| `abilities/AriaAbilityBase.cs` | 能力基类：Key/DisplayName/冷却 + `CanActivate`/`Activate`（返回动作）|
| `abilities/AriaAction.cs` | 动作载体：目标 → 效果字典 |
| `abilities/AriaAbilityHandle.cs` | 能力运行期句柄：冷却计时 + 所属实体，管理器直控 |
| `effects/AriaEffectBase.cs` | 效果基类：持续类型/时长/周期 + `Apply`/`Remove`/`Tick`/`GetTickEffects` |
| `effects/AriaEffectHandle.cs` | 效果运行期句柄：挂载信息 + 计时，管理器统一驱动 |
| `effects/AriaEffectDurationType.cs` | 效果持续类型枚举（Instant/HasDuration/Permanent）|
| `attributes/AriaAttributeOperation.cs` | 属性运算方式枚举（Add/Subtract/Multiply/Override）|
| `attributes/AriaAttributeModifier.cs` | 属性修饰器：AttributeKey + Operation + Magnitude |

### 游戏侧（不带 `Aria` 前缀）

| 文件 | 说明 |
|---|---|
| `scripts/Core/Interfaces/ICombatant.cs` | 战斗参与者接口（英雄/卡牌共同实现）：声明 `Abilities`/`Effects` |
| `scripts/Core/Interfaces/IPassiveAbility.cs` | 被动能力接口：声明响应的事件类型（`Type ReactEventType`）|
| `scripts/Core/Interfaces/IDamageEffect.cs` | 伤害效果接口：结算器据此识别并广播 DamageDealt |
| `scripts/Core/Interfaces/IHealEffect.cs` | 治疗效果接口：结算器据此触发净化（削减目标腐蚀/辐射）|
| `scripts/Combat/Contexts/BattleContext.cs` | 战斗上下文（继承 `AriaContextBase`，含当前事件 `CurrentEvent`）|
| `scripts/Combat/Events/CombatEventBase.cs` | 战斗事件抽象基类（按类型路由被动）|
| `scripts/Combat/Events/CombatEventBus.cs` | 全局战斗事件总线（静态观察者通道：DamageDealt/AbilityActivated/EffectApplied/HealthBelowHalf/NearDeath）|
| `scripts/Combat/Events/BattleStartEvent.cs` / `AbilityActivatedEvent.cs` / `AdjacentCardActivatedEvent.cs` / `EffectAppliedEvent.cs` / `DamageDealtEvent.cs` / `HealthBelowHalfEvent.cs` / `NearDeathEvent.cs` | 各触发事件（自带载荷）|
| `scripts/Combat/Events/DamageInfo.cs` | 伤害事件载荷（Source/Target/Amount/IsPiercing/AbsorbedByArmor）|

## 关键类定义

### `AriaAbilityBase`
```csharp
[GlobalClass]
public abstract partial class AriaAbilityBase : Resource
{
    public StringName Key { get; protected set; }       // 标识（不可变）
    public string DisplayName { get; protected set; }    // 展示名（不可变）
    public float CooldownSeconds { get; protected set; } // 冷却秒数
    public bool HasCooldown => CooldownSeconds > 0f;     // 是否有冷却

    public virtual bool CanActivate(AriaContextBase ctx) => true;   // 可选条件
    public virtual AriaAction[] Activate(AriaContextBase ctx) => []; // 返回目标+效果
}
```

### `AriaAction`
```csharp
// 一次发动产生的动作：目标 → 效果（目标可能多个）
public class AriaAction
{
    public Dictionary<IAriaEntity, AriaEffectBase[]> EffectsByTarget { get; set; } = new();
}
```

### `AriaEffectBase`
```csharp
[GlobalClass]
public abstract partial class AriaEffectBase : Resource
{
    public StringName Key { get; protected set; }
    public string DisplayName { get; protected set; }
    public AriaEffectDurationType DurationType { get; protected set; } // Instant/HasDuration/Permanent
    public float DurationSeconds { get; protected set; }   // HasDuration 用
    public float PeriodSeconds { get; protected set; }     // >0 → 周期 DoT/HoT（Instant 无效）
    public AriaAttributeModifier[] Modifiers { get; protected set; } = [];

    public virtual void Apply(AriaContextBase ctx) { }     // 应用（写修饰器/挂载）
    public virtual void Remove(AriaContextBase ctx) { }    // 移除（回滚）
    public virtual void Tick(float delta, AriaContextBase ctx) { }  // 周期跳动（管理器驱动）
    public virtual AriaEffectBase[] GetTickEffects(AriaContextBase ctx) => [this]; // 周期到期结算项
}
```

> **属性化 DoT/HoT**：辐射/腐蚀/再生值作为 `HeroAttributeSet` 属性（`Radiation`/`Corrosion`/`Regeneration`，值 = 每秒量），效果（`RadiationEffect`/`CorrosionEffect`/`RegenerationEffect`）为即时效果只负责加值。每帧由 CombatManager 的 `ApplyDot` 按值结算：辐射 → 穿透伤害 `值×dt` + 指数半衰（`×0.5^dt`）；腐蚀 → 普通伤害 `值×dt` + 线性衰减（`−1/s`）；再生 → 直接恢复生命 `值×dt` + 线性衰减（`−1/s`）。伤害复用统一结算管线入队伤害效果。
>
> **治疗净化**：结算 `IHealEffect`（如 `HealEffect`）后，削减目标 `Radiation`/`Corrosion` 属性值，**削减总量 = 治疗量 × 50%，按两属性当前值比例分摊**。

### `AriaAttributeModifier`
```csharp
[GlobalClass]
public partial class AriaAttributeModifier : Resource
{
    public string AttributeKey { get; set; }              // 目标属性
    public AriaAttributeOperation Operation { get; set; } // 加/减/乘/覆盖
    public float Magnitude { get; set; }
}
```

### `AriaAttributeSet`（扩展）
- 新增 `ApplyModifier(modifier)` / `RemoveModifier(modifier)`：按运算方式修改属性当前值并记录原值，移除时回滚；兼容现有直接 `SetCurrentValue`。

### `AriaAbilityHandle`
```csharp
public partial class AriaAbilityHandle : Resource
{
    public AriaAbilityBase Definition { get; }
    public IAriaEntity? Owner { get; }
    public float CooldownRemaining { get; private set; }
    public bool IsReady => CooldownRemaining <= 0f;

    public void UpdateCooldown(float delta);
    public void StartCooldown();
}
```

### `CombatEventBase`（游戏侧，按类型路由）
```csharp
// 战斗事件抽象基类：被动能力按 evt.GetType() 匹配 IPassiveAbility.ReactEventType。
public abstract class CombatEventBase { }

public class BattleStartEvent : CombatEventBase { }                        // 战斗开始
public class AbilityActivatedEvent : CombatEventBase                       // 能力发动
{
    public ICombatant? Combatant; public AriaAbilityBase Ability;
}
public class AdjacentCardActivatedEvent : CombatEventBase                  // 相邻卡牌能力发动
{
    public ICombatant Activator; public IReadOnlyList<ICombatant> AdjacentCards;
}
public class EffectAppliedEvent : CombatEventBase                          // 效果结算
{
    public ICombatant? Target; public AriaEffectBase Effect;
}
public class DamageDealtEvent : CombatEventBase                            // 造成伤害
{
    public DamageInfo Info;
}
public class HealthBelowHalfEvent : CombatEventBase                        // 首次跌破半血
{
    public ICombatant Combatant;
}
public class NearDeathEvent : CombatEventBase                              // 濒临死亡（首次跌破 25%）
{
    public ICombatant Combatant;
}
```

> 相邻事件：按所属方卡牌列表顺序（`FriendlyCards`/`EnemyCards`，即棋盘布局顺序）取左右邻居，无邻居不分发；濒死阈值常量 `NEAR_DEATH_THRESHOLD = 0.25`。

### `IPassiveAbility`（游戏侧）
```csharp
public interface IPassiveAbility
{
    Type ReactEventType { get; }   // 响应的事件类型，如 typeof(DamageDealtEvent)
}
```

### `ICombatant`（游戏侧）
```csharp
public interface ICombatant : IAriaEntity
{
    Godot.Collections.Array<AriaAbilityBase> Abilities { get; }
    Godot.Collections.Array<AriaEffectBase> Effects { get; }
}
```

### `BattleContext : AriaContextBase`（游戏侧）
```csharp
public partial class BattleContext : AriaContextBase
{
    public ICombatant? Source;
    public ICombatant? Target;
    public CombatEventBase? CurrentEvent;      // 当前分发的事件（被动读取载荷）

    public ICombatant? FriendlyHero;                  // 我方英雄（单个）
    public List<ICombatant> FriendlyCards;            // 我方卡牌
    public ICombatant? EnemyHero;                     // 敌方英雄（单个）
    public List<ICombatant> EnemyCards;               // 敌方卡牌
}
```

## 设计约定

- **命名**：Aria 前缀只用于 Aria 层类型；游戏侧不带 `Aria`。
- **接口**：统一放在 `interfaces/` / `Interfaces/` 文件夹，不放入 `Bases/`。
- **上下文继承**：Aria 提供 `AriaContextBase`，游戏侧继承出 `BattleContext`，能力/效果子类中按需 cast。
- **Aria 不定义具体功能**：动作类型（伤害/治疗/冻结等）语义全部由游戏侧能力定义，Aria 只提供通用载体。
- **能力子类写法**（游戏侧，如 `scripts/Entities/Abilities/`）：
```csharp
// 主动能力
public partial class PoisonAbility : AriaAbilityBase
{
    public override AriaAction[] Activate(AriaContextBase baseCtx)
    {
        var ctx = (BattleContext)baseCtx;
        return [ new AriaAction {
            EffectsByTarget = { [ctx.EnemyHero!] = [ new PoisonEffect() ] }
        } ];
    }
}

// 被动能力：实现 IPassiveAbility 声明响应事件类型，载荷经 ctx.CurrentEvent 读取
public partial class ThornsAbility : AriaAbilityBase, IPassiveAbility
{
    public Type ReactEventType => typeof(DamageDealtEvent);

    public override bool CanActivate(AriaContextBase baseCtx)
    {
        var ctx = (BattleContext)baseCtx;
        return ctx.CurrentEvent is DamageDealtEvent dmg && dmg.Info.Target is ICombatant enemy
            && ctx.FriendlyCards.Contains(enemy) == false; // 示例：仅我方受伤时反伤
    }

    public override AriaAction[] Activate(AriaContextBase baseCtx)
    {
        var ctx = (BattleContext)baseCtx;
        var dmg = (DamageDealtEvent)ctx.CurrentEvent!;
        return [ new AriaAction {
            EffectsByTarget = { [dmg.Info.Target] = [ new DamageEffect { DamageAmount = 3f } ] }
        } ];
    }
}
```
- **效果子类写法**（游戏侧，如 `scripts/Entities/Effects/`）：
```csharp
public partial class PoisonEffect : AriaEffectBase   // 周期毒：每秒扣血
{
    // DurationType = HasDuration; PeriodSeconds = 1f
    public override void Tick(float delta, AriaContextBase baseCtx)
    {
        var ctx = (BattleContext)baseCtx;
        // 每周期扣 ctx.Target 的 Health
    }
}
```
- **管理器通信边界**：管理器只与实体（`ICombatant`）通信，不直接驱动能力；能力冷却/编排统一由管理器（后续 `CombatManager`）处理。

## 待实现（后续阶段）

- 具体能力/效果模板（`scripts/Entities/Abilities/`、`scripts/Entities/Effects/`）。
- 战斗入场接线：由怪物/PvP 事件调用 `CombatManager.StartBattle`；目标选取约定（能力自治，用 ctx 双方列表自行选目标）。
- 连锁/总线的事件订阅清理约定（`EndBattle` 前断开外部订阅）。