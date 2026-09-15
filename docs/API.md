# API.md

项目通用接口文档。本文档记录项目中所有供外部调用的公共 API（接口、基类、状态容器、事件总线、枚举）。

## API 总览

| 类型 | 种类 | 层级 | 文件 | 核心职责 |
|---|---|---|---|---|
| `IEntity` | 接口 | 全局层 | `scripts/Core/Interfaces/IEntity.cs` | 实体深拷贝契约，供模板池调用 |
| `IAbility` | 接口 | 核心层 | `scripts/Core/Abilities/IAbility.cs` | 战斗动作单元，可复用的能力抽象 |
| `IEffect` | 接口 | 核心层 | `scripts/Core/Abilities/IEffect.cs` | 能力触发的被动效果（即时/周期） |
| `IBoardQuery` | 接口 | 核心层 | `scripts/Core/Abilities/IBoardQuery.cs` | 棋盘间接查询通道，解耦能力与棋盘 |
| `CardBase` | 抽象基类 | 全局层 | `scripts/Core/Bases/CardBase.cs` | 卡牌实体抽象基类 |
| `HeroBase` | 抽象基类 | 全局层 | `scripts/Core/Bases/HeroBase.cs` | 英雄实体抽象基类 |
| `EncounterBase` | 抽象基类 | 对局层 | `scripts/Core/Bases/EncounterBase.cs` | 遭遇实体抽象基类 |
| `AbilityBase` | 抽象基类 | 核心层 | `scripts/Core/Abilities/AbilityBase.cs` | 能力抽象基类，提供默认能量检查 |
| `EffectBase` | 抽象基类 | 核心层 | `scripts/Core/Abilities/EffectBase.cs` | 效果抽象基类，提供周期字段 |
| `AbilityContext` | 上下文 | 核心层 | `scripts/Core/Abilities/AbilityContext.cs` | 能力执行上下文 |
| `PoolBase<T>` | 泛型基类 | 全局层 | `scripts/Core/Pool/PoolBase.cs` | 反射模板池基类 |
| `StateContainer` | 抽象基类 | 核心层 | `scripts/Core/States/StateContainer.cs` | 字典驱动状态容器基类 |
| `AttributeSet` | 抽象基类 | 全局层 | `scripts/Core/States/AttributeSet.cs` | 对局资源容器基类 |
| `BattleState` | 抽象基类 | 战斗层 | `scripts/Core/States/BattleState.cs` | 战斗内临时状态容器基类 |
| `CardAttributeSet` | 状态容器 | 全局层 | `scripts/Core/States/CardAttributeSet.cs` | 卡牌对局资源容器 |
| `HeroAttributeSet` | 状态容器 | 全局层 | `scripts/Core/States/HeroAttributeSet.cs` | 英雄对局资源容器 |
| `EncounterAttributeSet` | 状态容器 | 对局层 | `scripts/Core/States/EncounterAttributeSet.cs` | 遭遇对局资源容器 |
| `CardBattleState` | 状态容器 | 战斗层 | `scripts/Core/States/CardBattleState.cs` | 卡牌战斗内临时状态 |
| `HeroBattleState` | 状态容器 | 战斗层 | `scripts/Core/States/HeroBattleState.cs` | 英雄战斗内临时状态 |
| `EventBase` | 抽象基类 | 共享层 | `scripts/Core/Events/EventBase.cs` | 总线事件根类型 |
| `EventBus<TEvent>` | 泛型类 | 共享层 | `scripts/Core/Events/EventBus.cs` | 强类型事件总线 |
| `MatchEvent` | 抽象基类 | 对局层 | `scripts/Match/Events/MatchEvent.cs` | 局外事件根类型 |
| `MatchEventBus` | 类 | 对局层 | `scripts/Match/Events/MatchEventBus.cs` | 局外事件总线 |
| `CombatEvent` | 抽象基类 | 战斗层 | `scripts/Combat/Events/CombatEvent.cs` | 战斗事件根类型 |
| `CombatEventBus` | 类 | 战斗层 | `scripts/Combat/Events/CombatEventBus.cs` | 战斗内事件总线 |
| `AbilityTrigger` | 枚举 | 核心层 | `scripts/Core/Abilities/AbilityTrigger.cs` | 能力触发方式 |
| `EffectType` | 枚举 | 核心层 | `scripts/Core/Abilities/EffectType.cs` | 效果类型 |
| `CardSize` | 枚举 | 全局层 | `scripts/Core/Types/CardSize.cs` | 卡牌型号尺寸 |

---

## 一、接口契约

### IEntity — 实体复制契约

**文件**：`scripts/Core/Interfaces/IEntity.cs`　**命名空间**：`Project_Star.Core.Interfaces`

**用途**：为所有需要"模板复制"的实体（英雄、卡牌等）定义深拷贝契约。模板池通过反射扫描所有实现此接口的非抽象子类，建立模板实例；玩家选择时调用 `Clone()` 生成独立实例（含独立属性集），保证每个玩家的英雄/卡牌互不干扰。

| 成员 | 签名 | 说明 |
|---|---|---|
| `Clone` | `object Clone()` | 复制独立实例（深拷贝属性集/战斗状态） |

### IAbility — 能力接口

**文件**：`scripts/Core/Abilities/IAbility.cs`　**命名空间**：`Project_Star.Core.Abilities`

**用途**：定义战斗中可被触发的动作单元。主动能力（手动触发/冷却）和被动能力（事件触发）都实现此接口，区别仅在于触发方式。能力只负责执行逻辑；触发、冷却、编排、连锁由战斗解析器驱动。约束见 `AGENTS.md`「硬约束」。

| 成员 | 类型 | 说明 |
|---|---|---|
| `AbilityKey` | `StringName` | 唯一标识 |
| `DisplayName` | `string` | 展示名 |
| `Trigger` | `AbilityTrigger` | 触发方式（主动/被动） |
| `EnergyCost` | `int` | 能量消耗（0 = 免能量） |
| `Cooldown` | `int` | 基础冷却时间 |
| `CanActivate(HeroBattleState caster)` | `bool` | 判断是否满足发动条件 |
| `Execute(AbilityContext context)` | `void` | 执行能力逻辑 |

### IEffect — 效果接口

**文件**：`scripts/Core/Abilities/IEffect.cs`　**命名空间**：`Project_Star.Core.Abilities`

**用途**：定义被能力触发的被动后果。效果分为两种类型：**即时结算**（一次性效果）和**周期 DoT/HoT**（持续性伤害/治疗）。效果由能力产生，战斗解析器按类型驱动结算；堆叠/到期扣减由战斗层管理。

| 成员 | 类型 | 说明 |
|---|---|---|
| `EffectKey` | `StringName` | 唯一标识 |
| `DisplayName` | `string` | 展示名 |
| `Type` | `EffectType` | 效果类型（即时/周期） |
| `Apply(AbilityContext context)` | `void` | 施加效果到目标 |

### IBoardQuery — 棋盘查询接口

**文件**：`scripts/Core/Abilities/IBoardQuery.cs`　**命名空间**：`Project_Star.Core.Abilities`

**用途**：为能力/效果提供间接查询棋盘信息的通道，由战斗层实现。能力不直接操作棋盘，通过此接口查询战场区和备战区的卡牌状态，遵循**迪米特法则**（最少知识原则），降低核心层与战斗层的耦合。

| 成员 | 签名 | 说明 |
|---|---|---|
| `GetBattlefieldCards` | `IReadOnlyList<CardBattleState> GetBattlefieldCards(HeroBattleState hero)` | 获取指定英雄所有战场区卡牌的战斗状态 |
| `GetBenchCards` | `IReadOnlyList<CardBattleState> GetBenchCards(HeroBattleState hero)` | 获取指定英雄所有备战区卡牌的战斗状态 |
| `GetAllCards` | `IReadOnlyList<CardBattleState> GetAllCards(HeroBattleState hero)` | 获取指定英雄的所有卡牌（战场 + 备战） |

---

## 二、实体基类

### CardBase — 卡牌实体抽象基类

**文件**：`scripts/Core/Bases/CardBase.cs`　**命名空间**：`Project_Star.Core.Bases`　**继承**：`RefCounted, IEntity`

**用途**：持卡牌对局资源与战斗状态，身份字段经属性集只读转发。子类在构造函数注入身份字段。**扩展点**：继承并传入 `CardAttributeSet`。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Attributes` | `CardAttributeSet` | 卡牌对局资源（身份字段 get-only + 等级/价值/对局加成/解锁记录） |
| `BattleState` | `CardBattleState` | 卡牌战斗内临时状态（战斗时派生） |
| `Clone()` | `virtual object` | 复制独立卡牌实例（供模板池调用） |

### HeroBase — 英雄实体抽象基类

**文件**：`scripts/Core/Bases/HeroBase.cs`　**命名空间**：`Project_Star.Core.Bases`　**继承**：`RefCounted, IEntity`

**用途**：持英雄对局资源与战斗状态，身份字段经属性集只读转发。子类在构造函数注入身份字段并覆写初始属性差异。**不建英雄场景**（逻辑层纯代码）。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Attributes` | `HeroAttributeSet` | 英雄对局资源（身份字段 get-only + 金钱/经验/等级/声望） |
| `BattleState` | `HeroBattleState` | 英雄战斗内临时状态（战斗时派生） |
| `Clone()` | `virtual object` | 复制独立英雄实例（供模板池调用） |

### EncounterBase — 遭遇实体抽象基类

**文件**：`scripts/Core/Bases/EncounterBase.cs`　**命名空间**：`Project_Star.Core.Bases`　**继承**：`RefCounted, IEntity`

**用途**：持遭遇对局资源。遭遇 = 玩家回合遭遇选项（商店/怪物战/PvP），与总线事件（MatchEvent/CombatEvent）区分。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Attributes` | `EncounterAttributeSet` | 遭遇对局资源（身份字段 get-only） |
| `Clone()` | `virtual object` | 复制独立遭遇实例（供模板池调用） |

---

## 三、能力基类与上下文

### AbilityBase — 能力抽象基类

**文件**：`scripts/Core/Abilities/AbilityBase.cs`　**命名空间**：`Project_Star.Core.Abilities`　**实现**：`IAbility`

**用途**：提供默认能量检查与属性自动实现。具体能力继承此基类，override `Execute` 实现逻辑；`CanActivate` 可选覆写扩展。能力不感知等级；卡牌根据等级设置属性值。

| 成员 | 类型 | 说明 |
|---|---|---|
| `AbilityKey` | `required StringName` | 唯一标识（init） |
| `DisplayName` | `required string` | 展示名（init） |
| `Trigger` | `AbilityTrigger` | 触发方式（init） |
| `EnergyCost` | `int` | 能量消耗（init） |
| `Cooldown` | `int` | 基础冷却时间（init） |
| `CanActivate(HeroBattleState caster)` | `virtual bool` | 默认检查能量是否足够；覆写时须 `base.CanActivate && 自定义条件` |
| `Execute(AbilityContext context)` | `abstract void` | 执行能力逻辑（子类实现） |

### EffectBase — 效果抽象基类

**文件**：`scripts/Core/Abilities/EffectBase.cs`　**命名空间**：`Project_Star.Core.Abilities`　**实现**：`IEffect`

**用途**：提供周期效果所需的时长/周期/贡献量字段。即时效果 `Duration = 0`、`TickInterval = 0`；周期效果由战斗解析器按时钟驱动。堆叠约定由战斗层管理，基类不实现（规则见 `docs/GAME_DESIGN.md` 第九节）。

| 成员 | 类型 | 说明 |
|---|---|---|
| `EffectKey` | `required StringName` | 唯一标识（init） |
| `DisplayName` | `required string` | 展示名（init） |
| `Type` | `EffectType` | 效果类型（init） |
| `Duration` | `int` | 剩余持续时长（即时为 0） |
| `TickInterval` | `int` | 触发周期（即时为 0，单位为逻辑步） |
| `ContributionAmount` | `int` | 本次施加贡献量（用于到期扣减） |
| `Apply(AbilityContext context)` | `abstract void` | 施加效果到目标（子类实现） |

### AbilityContext — 能力执行上下文

**文件**：`scripts/Core/Abilities/AbilityContext.cs`　**命名空间**：`Project_Star.Core.Abilities`

**用途**：封装能力执行所需的所有信息，避免参数列表膨胀。新增上下文字段不改接口签名，已实现的能力不受影响（开闭原则）。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Caster` | `required HeroBattleState` | 施法者英雄战斗状态（init；主动=发动方，被动=拥有者） |
| `Target` | `HeroBattleState?` | 目标英雄战斗状态（默认敌方，效果可覆写） |
| `OwnerCard` | `CardBattleState?` | 拥有此能力的卡牌战斗状态（主动填入，被动可能为 null） |
| `TriggerEvent` | `object?` | 触发事件引用（类型为 object 避免 Core 依赖 Combat 层） |
| `Board` | `IBoardQuery?` | 棋盘查询接口（由战斗层注入实现） |

---

## 四、模板池

### PoolBase<T> — 反射模板池基类

**文件**：`scripts/Core/Pool/PoolBase.cs`　**命名空间**：`Project_Star.Core.Pool`　**约束**：`where T : RefCounted, IEntity`

**用途**：用反射扫描程序集收集所有非抽象实体子类各建一个作模板；创建时复制独立实例。子类指定 `T` 即可获得对应实体的模板池。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Templates` | `IReadOnlyList<T>` | 模板列表（每个非抽象子类各一个） |
| `CreateFromTemplate(T template)` | `T` | 从模板复制独立实例（供选择/创建实体时调用） |

---

## 五、状态容器

### StateContainer — 状态容器基类

**文件**：`scripts/Core/States/StateContainer.cs`　**命名空间**：`Project_Star.Core.States`

**用途**：字典驱动的属性存取，供对局资源（`AttributeSet`）与战斗状态（`BattleState`）共用。数值以 `StringName` 为 key 存入字典。

| 成员 | 签名 | 说明 |
|---|---|---|
| `GetValue` | `int GetValue(StringName key)` | 读取属性值；不存在返回 0 |
| `SetValue` | `void SetValue(StringName key, int value)` | 覆盖设置属性值 |
| `ApplyModifier` | `void ApplyModifier(StringName key, int amount)` | 累加属性值（数值流转统一入口） |
| `RemoveModifier` | `void RemoveModifier(StringName key, int amount)` | 扣减属性值（数值流转统一入口） |
| `Clone` | `virtual StateContainer Clone()` | 深拷贝状态容器（子类须 override 返回自身类型） |

### AttributeSet — 对局资源容器基类

**文件**：`scripts/Core/States/AttributeSet.cs`　**命名空间**：`Project_Star.Core.States`　**继承**：`StateContainer`

**用途**：跨战斗保留的实体属性。**扩展点**：子类实现具体属性。

| 成员 | 签名 | 说明 |
|---|---|---|
| `Clone` | `override StateContainer Clone()` | 深拷贝属性集（供模板池复制实例时调用） |

### BattleState — 战斗内临时状态容器基类

**文件**：`scripts/Core/States/BattleState.cs`　**命名空间**：`Project_Star.Core.States`　**继承**：`StateContainer`

**用途**：每场战斗开始派生初始值、结束丢弃。**扩展点**：子类实现具体战斗属性。

| 成员 | 签名 | 说明 |
|---|---|---|
| `Clone` | `override StateContainer Clone()` | 深拷贝战斗状态（供战斗初始值派生时调用） |

### CardAttributeSet — 卡牌对局资源容器

**文件**：`scripts/Core/States/CardAttributeSet.cs`　**命名空间**：`Project_Star.Core.States`　**继承**：`AttributeSet`

**用途**：身份字段（get-only，构造注入）+ 等级/价值/对局加成/解锁记录。

| 成员 | 类型 | 说明 |
|---|---|---|
| `CardKey` | `StringName` | 卡牌唯一标识（get-only） |
| `DisplayName` | `string` | 展示名（get-only） |
| `FactionKey` | `StringName` | 归属 key（get-only） |
| `Size` | `CardSize` | 型号尺寸（get-only） |
| `Level` | `int` | 等级 |
| `Value` | `int` | 价值 |
| `PersistentBonus` | `int` | 对局加成 |
| `UnlockRecord` | `HashSet<StringName>` | 解锁记录（对局内按 key 跟踪） |
| 构造 | `CardAttributeSet(StringName cardKey, string displayName, StringName factionKey, CardSize size)` | 注入身份字段 |
| `Clone` | `override AttributeSet` | 深拷贝（含解锁记录） |

### HeroAttributeSet — 英雄对局资源容器

**文件**：`scripts/Core/States/HeroAttributeSet.cs`　**命名空间**：`Project_Star.Core.States`　**继承**：`AttributeSet`

**用途**：身份字段（get-only，构造注入）+ 金钱/经验/等级/声望。

| 成员 | 类型 | 说明 |
|---|---|---|
| `HeroKey` | `StringName` | 英雄唯一标识（get-only） |
| `HeroDisplayName` | `string` | 展示名（get-only） |
| `FactionKey` | `StringName` | 阵营 key（get-only） |
| `Wealth` | `int` | 金钱 |
| `Experience` | `int` | 经验 |
| `Level` | `int` | 等级 |
| `Reputation` | `int` | 声望 |
| 构造 | `HeroAttributeSet(StringName heroKey, string heroDisplayName, StringName factionKey)` | 注入身份字段 |

### EncounterAttributeSet — 遭遇对局资源容器

**文件**：`scripts/Core/States/EncounterAttributeSet.cs`　**命名空间**：`Project_Star.Core.States`　**继承**：`AttributeSet`

**用途**：身份字段（get-only，构造注入），暂无可变字段。

| 成员 | 类型 | 说明 |
|---|---|---|
| `EncounterKey` | `StringName` | 遭遇唯一标识（get-only） |
| `DisplayName` | `string` | 展示名（get-only） |
| 构造 | `EncounterAttributeSet(StringName encounterKey, string displayName)` | 注入身份字段 |

### CardBattleState — 卡牌战斗内临时状态

**文件**：`scripts/Core/States/CardBattleState.cs`　**命名空间**：`Project_Star.Core.States`　**继承**：`BattleState`

**用途**：冷却/超频/麻痹/禁锢/是否被摧毁等，战斗结束丢弃。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Bonus` | `int` | 战斗内加成 |
| `Cooldown` | `int` | 冷却 |
| `OverclockDuration` | `int` | 超频剩余时长 |
| `ParalysisDuration` | `int` | 麻痹剩余时长 |
| `ImmobilizeDuration` | `int` | 禁锢剩余时长 |
| `Destroyed` | `bool` | 是否被摧毁 |

### HeroBattleState — 英雄战斗内临时状态

**文件**：`scripts/Core/States/HeroBattleState.cs`　**命名空间**：`Project_Star.Core.States`　**继承**：`BattleState`

**用途**：生命/护甲/辐射/腐蚀/能量等，战斗结束丢弃。

| 成员 | 类型 | 说明 |
|---|---|---|
| `Health` | `int` | 当前生命 |
| `MaxHealth` | `int` | 最大生命 |
| `Armor` | `int` | 护甲 |
| `Radiation` | `int` | 辐射 |
| `Corrosion` | `int` | 腐蚀 |
| `HealthRegen` | `int` | 生命再生 |
| `Energy` | `int` | 当前能量 |
| `MaxEnergy` | `int` | 最大能量 |
| `EnergyRegen` | `int` | 能量再生 |

---

## 六、事件总线

### EventBase — 总线事件根类型

**文件**：`scripts/Core/Events/EventBase.cs`　**命名空间**：`Project_Star.Core.Events`

**用途**：所有事件总线消息的根类型，具体事件继承 `MatchEvent` / `CombatEvent`。

### EventBus<TEvent> — 强类型事件总线

**文件**：`scripts/Core/Events/EventBus.cs`　**命名空间**：`Project_Star.Core.Events`　**约束**：`where TEvent : EventBase`

**用途**：强类型事件按 Type 分发，订阅返回退订令牌，发布沿继承链分发给基类处理器。

| 成员 | 签名 | 说明 |
|---|---|---|
| `Subscribe<T>` | `IDisposable Subscribe<T>(Action<T> handler) where T : TEvent` | 订阅事件；返回令牌，`Dispose` 即退订 |
| `Publish<T>` | `void Publish<T>(T evt) where T : TEvent` | 发布事件；沿继承链分发给已订阅的基类处理器 |

### MatchEvent / MatchEventBus — 局外事件

**文件**：`scripts/Match/Events/MatchEvent.cs`、`MatchEventBus.cs`　**命名空间**：`Project_Star.Match.Events`

**用途**：对局内总线消息的根类型与总线，与战斗事件（CombatEvent）分离，两者互不互通。

- `MatchEvent`：抽象基类（继承 `EventBase`）
- `MatchEventBus`：`sealed class`，继承 `EventBus<MatchEvent>`

### CombatEvent / CombatEventBus — 战斗事件

**文件**：`scripts/Combat/Events/CombatEvent.cs`、`CombatEventBus.cs`　**命名空间**：`Project_Star.Combat.Events`

**用途**：战斗内总线消息的根类型与总线，战斗逻辑通信与被动触发，与局外总线互不互通。

- `CombatEvent`：抽象基类（继承 `EventBase`）
- `CombatEventBus`：`sealed class`，继承 `EventBus<CombatEvent>`

---

## 七、枚举

### AbilityTrigger — 能力触发方式

**文件**：`scripts/Core/Abilities/AbilityTrigger.cs`　**命名空间**：`Project_Star.Core.Abilities`

| 值 | 说明 |
|---|---|
| `Active` | 主动：冷却到期后自动发动 |
| `Passive` | 被动：监听战斗事件触发 |

### EffectType — 效果类型

**文件**：`scripts/Core/Abilities/EffectType.cs`　**命名空间**：`Project_Star.Core.Abilities`

| 值 | 说明 |
|---|---|
| `Instant` | 即时：施加后立即结算（伤害/治疗/护甲） |
| `Periodic` | 周期：按时长与周期持续结算（辐射/腐蚀/超频等） |

### CardSize — 卡牌型号尺寸

**文件**：`scripts/Core/Types/CardSize.cs`　**命名空间**：`Project_Star.Core.Types`

| 值 | 数值 | 说明 |
|---|---|---|
| `Small` | 1 | 小 |
| `Medium` | 2 | 中 |
| `Large` | 3 | 大 |
