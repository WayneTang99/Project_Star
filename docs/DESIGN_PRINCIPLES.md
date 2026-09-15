# DESIGN_PRINCIPLES.md
Project_Star 代码层遵循的软件设计原则。供 AI 代理 / 开发者编写与审查代码时参考；与 `AGENTS.md`（需求）、`docs/ARCHITECTURE.md`（架构）配合使用。

## 一、SOLID 原则

### 1. 单一职责原则（SRP — Single Responsibility Principle）

> 一个类只有一个引起它变化的原因。

- 一个类只做一件事，做好一件事。
- 属性集（AttributeSet）只管数值存取，不包含业务逻辑。
- 能力（Ability）只负责执行逻辑，触发与编排由战斗解析器负责。
- 管理器之间职责不重叠：CardManager 管卡牌实例与数据，BoardManager 管棋盘位置与布局。

### 2. 开闭原则（OCP — Open/Closed Principle）

> 对扩展开放，对修改关闭。

- 新增能力类型：继承 `AbilityBase`，override `Execute`，不改现有代码。
- 新增效果类型：继承 `EffectBase`，override `Apply`，不改现有代码。
- 新增事件：继承 `CombatEvent` / `MatchEvent`，发布到总线，不改现有事件处理器。
- 新增实体子类：继承 `HeroBase` / `CardBase`，反射模板池自动收集，不改池逻辑。
- 新增战斗结束原因：扩展 `BattleEndReason` 枚举，不改战斗结束判断逻辑。
- **判断标准**：如果新增功能需要修改现有类的代码，说明设计未遵循 OCP。

### 3. 里氏替换原则（LSP — Liskov Substitution Principle）

> 子类必须能替换父类使用，且不改变程序的正确性。

- 所有 `AbilityBase` 子类必须能安全地替代 `AbilityBase` 使用。
- `CanActivate` 的覆写不能改变默认契约：基类检查能量，子类扩展条件但不能绕过能量检查。
- `Execute` 的覆写必须完成能力应做的事，不能有副作用破坏调用方预期。
- `Clone` 返回的对象必须与原对象行为一致（深拷贝、独立属性集）。

### 4. 接口隔离原则（ISP — Interface Segregation Principle）

> 不应强迫客户端依赖它不使用的接口。

- `IEntity` 只定义 `Clone`，不强制所有实体实现不需要的方法。
- `IAbility` 和 `IEffect` 分离，能力不持有效果引用，效果不持有能力引用。
- 未来扩展接口（如 `IBoardQueryable`）按需实现，不强制所有实体实现。

### 5. 依赖倒置原则（DIP — Dependency Inversion Principle）

> 高层模块不应依赖低层模块，两者都应依赖抽象。

- 战斗解析器依赖 `IAbility` / `IEffect` 接口，不依赖具体能力子类。
- 事件总线泛型约束为 `EventBase`，不依赖具体事件类型。
- 管理器间通过事件通信，不直接互调具体类。

## 二、其他设计原则

### 组合优于继承（Composition over Inheritance）

- 一张卡由多个已有能力组合而成，通过参数差异化；不为单张卡写专属能力类。
- 能力复用约定：只有当现有能力组合无法表达所需逻辑时，才新建能力类。
- 效果通过能力产生，能力不持有效果列表；效果的组合与编排由战斗解析器负责。

### 参数对象模式（Parameter Object）

- 能力执行上下文（`AbilityContext`）封装施法者/目标/战场等信息，避免参数列表膨胀。
- 新增上下文字段不改接口签名，已实现的能力不受影响。

### 最小知识原则（Principle of Least Knowledge / 迪米特法则）

- 能力不直接操作棋盘，通过 context 间接查询。
- UI 只读管理器状态、订阅事件刷新，不直接改模型。
- 管理器间通过事件通信，避免互相直调。

### 好莱坞原则（Hollywood Principle / 控制反转）

> Don't call us, we'll call you.

- 能力不主动调用战斗解析器，被动能力由事件触发被动调用。
- UI 不主动轮询状态，订阅事件被动刷新。
- 棋盘数据不主动通知 UI，通过事件总线广播变更。

## 三、本项目扩展点速查

| 扩展需求 | 扩展方式 | 参见 |
|---|---|---|
| 新增能力类型 | 继承 `AbilityBase`，override `Execute` | ARCHITECTURE.md「四、能力系统」 |
| 新增效果类型 | 继承 `EffectBase`，override `Apply` | ARCHITECTURE.md「四、能力系统」 |
| 能力需要棋盘信息 | 在 `AbilityContext` 加 `Board` 属性 | ARCHITECTURE.md「四、能力系统」 |
| 能力监听战斗事件 | `AbilityContext` 加 `Event` 属性 | ARCHITECTURE.md「四、能力系统」 |
| 新增战斗事件 | 继承 `CombatEvent`，发布到 `CombatEventBus` | ARCHITECTURE.md「二、分层与管理器」 |
| 新增对局事件 | 继承 `MatchEvent`，发布到 `MatchEventBus` | ARCHITECTURE.md「二、分层与管理器」 |
| 新增英雄子类 | 继承 `HeroBase`，反射模板池自动收集 | ARCHITECTURE.md「三、实体与状态」 |
| 新增卡牌子类 | 继承 `CardBase`，反射模板池自动收集 | ARCHITECTURE.md「三、实体与状态」 |
| 新增战斗结束原因 | 扩展 `BattleEndReason` 枚举 | ARCHITECTURE.md「五、战斗系统」 |
| 新增遭遇类型 | 继承 `EncounterBase`，反射模板池自动收集 | ARCHITECTURE.md「三、实体与状态」 |
