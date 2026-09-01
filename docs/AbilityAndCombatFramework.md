# 能力与战斗框架设计

本文档记录能力（主动技能/被动技能）与战斗相关框架的设计与实现约定。战斗管理器尚未实现，本阶段已完成能力与效果相关的 Aria 抽象及游戏侧接线。

## 核心模型：能力触发效果

- **能力（Ability）**：可被触发的动作单元。主动（冷却/手动）与被动（事件触发）都是能力，区别仅在于触发方式，由管理器决定。能力类只负责**执行逻辑**。
- **效果（Effect）**：被能力触发的被动后果（静态修饰 / 周期 DoT-HoT / 即时 / 永久）。
- 触发、冷却、编排、连锁（≤3 层上限）统一由后续 `CombatManager` 负责；管理器只与实体（`ICombatant`）通信，不直接驱动能力。

## 管理器单帧流程（CombatManager 阶段实现）

```
1. 计时（冷却递减）
2. 查冷却 → 主动能力就绪 → 调 ability.Activate(ctx) → 收集 actions
3. 广播本次发动（来源能力 + actions）→ 触发订阅的被动能力（连锁 ≤3 层计数阻断）→ 再收集 actions
4. 统一执行所有收集到的 actions：对每个 (目标, 效果)，设 ctx.Target 后执行效果
```

- 先**收集**再**执行**，避免效果执行中途再触发能力造成链式循环。

## 分层

### Aria 层（`addons/aria/`，游戏无关，保留 `Aria` 前缀）

| 文件 | 说明 |
|---|---|
| `interfaces/IAriaEntity.cs` | 最小实体接口：暴露 `AriaAttributeSet AttributeSet` |
| `contexts/AriaContextBase.cs` | 上下文基类，供游戏侧继承自定义 |
| `abilities/AriaAbilityBase.cs` | 能力基类：Key/DisplayName/冷却 + `CanActivate`/`Activate`（返回动作）|
| `abilities/AriaAction.cs` | 动作载体：目标 → 效果字典 |
| `abilities/AriaAbilityHandle.cs` | 能力运行期句柄：冷却计时 + 所属实体，管理器直控 |
| `effects/AriaEffectBase.cs` | 效果基类：持续类型/时长/周期 + `Apply`/`Remove`/`Tick` |
| `effects/AriaEffectDurationType.cs` | 效果持续类型枚举（Instant/HasDuration/Permanent）|
| `attributes/AriaAttributeOperation.cs` | 属性运算方式枚举（Add/Subtract/Multiply/Override）|
| `attributes/AriaAttributeModifier.cs` | 属性修饰器：AttributeKey + Operation + Magnitude |

### 游戏侧（`scripts/Core/`，不带 `Aria` 前缀）

| 文件 | 说明 |
|---|---|
| `Interfaces/ICombatant.cs` | 战斗参与者接口（英雄/卡牌共同实现）：声明 `Abilities`/`Effects` |
| `Types/TriggerType.cs` | 触发方式枚举：None/主动、BattleStart、CardActivated、HealthBelowHalf |
| `Contexts/BattleContext.cs` | 战斗上下文（继承 `AriaContextBase`）|

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
}
```

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

### `TriggerType`（游戏侧）
```csharp
public enum TriggerType
{
    None,            // 主动（冷却/手动发动）
    BattleStart,     // 战斗开始
    CardActivated,   // 某卡牌发动
    HealthBelowHalf, // 生命值过半
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
    public TriggerType TriggerType;

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

- `CombatManager`：收集能力句柄、全局 `TriggerType` 索引、冷却池、广播事件、**连锁 ≤3 层计数阻断**、周期效果驱动、目标选取、结算。
- 具体能力/效果模板。