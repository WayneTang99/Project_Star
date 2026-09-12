# AGENTS.md

Project_Star 的代理工作指南。本文件供 AI 代理 / 开发者了解项目约定，请遵守其中的规则。

## 项目概览

- 基于 **Godot 4.7** 的 3D 游戏项目
- 使用 **C# / .NET** 编写脚本（dotnet 模块，程序集名 `Project_Star`）
- 物理引擎：**Jolt Physics**
- 渲染：**Forward Plus**，Windows 下使用 **D3D12** 驱动
- 视口拉伸模式：`canvas_items` + `expand` 自适应
- 类型：类《The Bazaar》（大巴扎）的**卡牌异步对战自走棋**
- 玩法框架：自研 **Aria** 插件（基于 Forge for Godot 思路参考，**不得引用 Forge 运行时**）

## 游戏设计（立项）

### 核心要素

- **英雄（Hero）**：玩家选择的角色，携带属性集，是构筑与战斗的主体。
- **卡牌（Card）**：构成角色构筑与战斗手段的核心资源，可通过商店交易获得。
- **事件（Event）**：回合中出现的各种状况，可能为单事件或多选一。

### 游戏流程

- 玩家**选择英雄**进入游戏。
- 游戏以 **轮（Round）** + **回合（Turn）** 逐步递进，每回合可能出现各种事件。
- **商店事件**中，玩家可以**交易卡牌**。
- 玩家拥有**备战区（Bench）** 与 **战场区（Battlefield）**，可随意放置卡牌。
- **默认回合**为**三选一事件组**，其中**至少 1 个商店事件**。
- **每轮第 4 回合**：3 个怪物事件，三选一挑一个进入战斗。
- **8 回合为 1 轮**，每轮**最后一回合（第 8 回合）固定为异步玩家对战事件**。
- 玩家对战事件与怪物对战事件点击后**进入战斗**，战斗使用**战场区卡牌**自动进行。

### 管理器架构

管理器**不注册为 Autoload、不作为场景子节点**，由根节点脚本 `scripts/Core/Main.cs` 在 `_Ready` 中 **new 生成并 AddChild 挂载**、统一注册。管理器按子系统分目录：元游戏（`scripts/Systems/`）、棋盘（`scripts/Board/Manager/`）、局内轮次与事件（`scripts/Match/`）、战斗（`scripts/Combat/Managers/`）。`scripts/Core/` 只放**共享定义**（抽象基类 / 接口 / 非实体类型定义）与组合根 `Main.cs`，不落具体实现。

```
Main (主场景根节点，挂 Main.cs，_Ready 生成并缓存各管理器引用)
├── GameManager              # 主状态机（Systems）：选英雄 → 局内 → 结算（信号广播状态切换；EndMatch/Surrender 强制结束总线）
├── HeroManager              # 英雄（Systems）：持有英雄模板池（每种各一个）、选择后复制实例、属性存取
├── CardManager              # 卡牌（Systems）：反射收集模板池、实例化、玩家拥有卡牌、按阵营key过滤供商店
├── BoardManager             # 棋盘（Board/Manager）：战场/备战区双棋盘（各10格）、推挤放置/移除/交换/跨区拖拽编排
├── CombatManager            # 战斗（Combat/Managers）：持有 BattleClock + Resolver，管理战斗生命周期 + A/B 超时
└── MatchManager             # 对局管理器（Match）：整合轮次(RoundTurn)、事件管理(EventManager)、事件执行(EventExecutor)、局外被动路由(OutOfBattleTrigger)
```

**通信与数据约定**

- 管理器间通过 **Godot 信号（C# event）** 通信，避免互相直调。
- 数值流转一律走 `AriaAttributeSet`（扣财富、扣血等），不直接改字段。
- `CardManager` 管卡牌实例与数据，`BoardManager` 管卡牌在棋盘上的位置与布局。
- 实体基类（`HeroBase` / `CardBase` / `EventBase` 等）与共享定义放 `scripts/Core/`，供各系统与 UI 复用。

## 目录结构

```
Project_Star/
├── .godot/            # Godot 缓存（忽略）
├── assets/            # 资源文件
│   ├── sprites/       # 精灵图 (.png, .svg)
│   ├── sounds/        # 音效 (.wav, .ogg)
│   ├── models/        # 3D模型 (.glb, .fbx)
│   └── fonts/         # 字体 (.ttf)
├── addons/            # Godot 插件
│   ├── aria/          # Aria 自研玩法插件（属性、能力、效果等）
│   │   ├── interfaces/  # Aria 接口（IAriaEntity）
│   │   ├── attributes/  # Aria 属性（AriaAttributeData / AriaAttributeSet）
│   │   ├── contexts/    # Aria 上下文基类（AriaContextBase）
│   │   ├── abilities/   # Aria 能力（AriaAbilityBase / AriaAbilityHandle）
│   │   ├── effects/     # Aria 效果（AriaEffectBase）
│   │   ├── expressions/ # 动态值表达式（ValueExpression / Condition）
│   │   └── definitions/ # 数据驱动定义（AbilityDefinition / EffectDefinition / DataDrivenAbility / EffectFactory）
│   └── forge/         # Forge for Godot 插件（仅作参考，不引用）
├── scenes/            # 场景文件 (.tscn)
│   ├── Main.tscn      # 主场景
│   ├── Menu.tscn      # 菜单场景
│   └── Gameplay/      # 游戏玩法场景
├── scripts/           # C# 脚本 (.cs)
│   ├── Core/          # 共享定义/契约层（仅抽象基类、接口、非实体类型定义、Main）
│   │   ├── Main.cs    # 组合根：_Ready 生成并挂载各子系统管理器（唯一引用具体管理器的文件）
│   │   ├── Bases/     # 抽象基类（HeroBase / CardBase / EventBase / MonsterBase / ManagerBase）
│   │   ├── Interfaces/  # 游戏侧接口（ICombatant / IDamageEffect / IPassiveAbility / IEntity）
│   │   ├── Types/     # 非实体类型定义（GameState / CardState / HeroState / EventState）
│   │   ├── AttributeSets/  # 属性集（Hero / Card / Event）
│   │   └── Pools/     # 泛型池（PoolBase<T>）
│   ├── Systems/       # 元游戏管理器（GameManager / HeroManager / CardManager）
│   ├── Board/         # 棋盘子系统（Manager/BoardManager、Data/GameBoard|BoardEntry、Algo/BoardUtil|Push*|BoardState、CardSizeExtensions）
│   ├── Match/         # 对局管理器（MatchManager + 普通类：RoundTurn / EventManager / EventExecutor / OutOfBattleTrigger）
│   ├── Combat/        # 战斗子系统（Managers/CombatManager、BattleClock / Resolver / BattleSnapshot、Contexts/BattleContext、Events/战斗事件）
│   ├── Entities/      # 实体子类（Heroes/TemplateHero、Cards/TemplateCard、Events/TemplateEvent、Abilities/能力、Effects/效果 等）
│   └── UI/            # UI控制器（MinimalGameUI / HeroSelectionUI / InMatchHeroUI 等）
├── shaders/           # 着色器 (.gdshader)
├── tests/             # 单元测试
├── test_ui/           # 独立测试UI（棋盘推挤演示，核心不引用，仅单独打开场景运行）
├── Project_Star.csproj # C# 项目文件
└── Project_Star.sln   # 解决方案文件
```

- 新脚本放 `scripts/` 对应模块目录；新场景放 `scenes/`，命名与对应脚本保持一致。
- `.godot/` 由编辑器生成，禁止手动修改，已加入 `.gitignore`。

## 构建与运行

```powershell
dotnet build                    # 编译 C# 脚本
# 运行：用 Godot 4.7 (mono 版) 打开项目目录，或：
godot --path .                  # 编辑器可执行文件已配置好 mono 模块
```

## Forge for Godot 插件（仅参考）

**Forge for Godot**（Unreal GAS 风格的游戏玩法框架，仅支持 C#）只作为 Aria 插件的**设计参考**。

### 注意事项

- ⚠️ **本项目不得引用 Forge 运行时**：不 `using Gamesmiths.Forge.*`，不依赖 `Forge.props` / NuGet 包，不为 Forge 写业务代码。
- ⚠️ 插件位于 `addons/forge/`，仅保留用于阅读参考，其源码问题不修复、不依赖。
- 改动前先参考 `addons/forge/` 与 NuGet 包 `Gamesmiths.Forge 0.4.0` 的设计思路（如 Ability、Attribute 结构）。

## Aria 插件（自研）

`addons/aria/` 是自研玩法插件，承接 Forge 思路但**独立实现，不依赖 Forge**。

### 插件定位（重要）

- ⚠️ Aria 是**跨游戏复用的通用玩法插件**，与具体游戏项目无关，后期可应用于其他游戏。
- ⚠️ **插件内不得包含游戏专属逻辑**：不引用 `scripts/`、不依赖具体管理器（`GameManager` 等）、不感知英雄/卡牌/事件等游戏概念。
- 游戏专属逻辑（管理器、实体基类）放 `scripts/Core/`，由主场景注册；插件只提供通用框架（属性、能力等）。
- 插件可导出为独立模块（如 Godot Asset Library 打包）供多项目复用。

### 属性系统（已建）

- `attributes/AriaAttributeData`：单个属性（BaseValue / CurrentValue / MinValue / MaxValue，值变更事件，Min/Max 联动校正）。
- `attributes/AriaAttributeSet`：属性集合（字典管理，按 key 增查改）。

### 项目侧对 Aria 的扩展（已建）

- `scripts/Core/AttributeSets/HeroAttributeSet`：英雄属性集（继承 `AriaAttributeSet`，含**不可变**身份字段 `HeroKey`/`HeroDisplayName`/`FactionKey` 与生命 / 护甲 / 财富 / 经验 / 等级 / 声望 / **辐射** / **腐蚀** / **生命再生** / **麻痹时长**）。
- `scripts/Core/Bases/HeroBase`：英雄基类（Node，持有 `HeroAttributeSet`，`ApplyInitialAttributes` 供子类覆写）。
- `scripts/Entities/Heroes/TemplateHero`：示例英雄（继承 `HeroBase`，覆写初始属性）。
- `scripts/Systems/HeroManager`：英雄管理器（**反射**收集所有非抽象 `HeroBase` 子类作模板池、`SelectHero` 复制实例、`HeroSelectedEvent`/`HeroResetEvent`）。
- `scripts/Core/Types/CardEconomy`：卡牌经济常量与价值公式（普通卡价值系数 2、购买折半倍率 0.5、型号系数 小1/中2/大3、`InitialValue`）。
- `scripts/Core/AttributeSets/CardAttributeSet`：卡牌属性集（继承 `AriaAttributeSet`，含**不可变**身份字段 `CardKey`/`DisplayName`/`HeroKey`/`Size` 与可变 `Level`、`Value`、`Cooldown`）。
- `scripts/Core/Bases/CardBase`：卡牌抽象基类（Node，持有 `CardAttributeSet`；提供 `ValueScale`/`GetInitialValue`/`InitializeValue` 支撑价值公式）。
- `scripts/Entities/Cards/TemplateCard`：示例卡牌（继承 `CardBase`）。
- `scripts/Entities/Cards/BeastHideCard`：兽皮卡（价值系数 4、**无能力**、归属中立 key `Neutral` → 不进入按英雄过滤的商店货架；作怪物掉落/奖励道具，加入玩家卡池后出售换钱）。
- `scripts/Systems/CardManager`：卡牌管理器（**反射**收集模板池、`CreateCard` 复制实例、`AddCardToPlayer`、`GetCardsByFaction` 供商店过滤）。
- `scripts/Core/Types/GameState` + `scripts/Systems/GameManager`：主状态机（`StateChangedEvent` 信号广播，`EndMatch`/`Surrender` 强制结束总线）。
- `scripts/Core/Types/GameState`（含 `MatchEndReason`）+ `scripts/Match/MatchManager`：对局管理器（整合轮次 `RoundTurn`、事件管理 `EventManager`（普通类）、事件执行 `EventExecutor`、局外被动路由 `OutOfBattleTrigger`）。
- `scripts/Core/Types/EventState` + `scripts/Core/AttributeSets/EventAttributeSet`：事件属性集（继承 `AriaAttributeSet`，含**不可变**身份字段 `EventKey`/`EventDisplayName` 与**等级（1~5）**、**轮次范围**（`MinRound`/`MaxRound`）属性）。
- 怪物直接复用 `HeroAttributeSet`（不另建怪物属性集）。
- `scripts/Core/Bases/EventBase`：事件抽象基类（Node，持有 `EventAttributeSet`，`State` 生命周期，`Initialize`/`OnResolve` 供子类覆写）。
- 怪物事件不做中间态，仅一个 `scripts/Entities/Events/MonsterEvent` 叶子类直接继承 `EventBase`，并引用 `HeroBase` 作怪物实体（怪物继承 `HeroBase`，与英雄同样持有属性集与卡牌）。
- `scripts/Match/EventManager`：事件管理器（**普通类**，反射收集模板池、排程生成事件组、`CreateEvent`/`AddEvent`/`ClearEvents`/`ResolveEvent`，持有 `MonsterEventManager` 子管理器）。
- `scripts/Entities/Events/TemplateEvent` / `TemplateShopEvent`：示例事件（通用 / 商店）；`scripts/Entities/Events/MonsterEvent`：怪物对战事件叶子类（引用 `HeroBase` 作怪物）。
- `scenes/Main.tscn` + `scripts/Core/Main.cs`：主场景根节点，`_Ready` 中 new 生成并挂载各管理器。

### 事件排程（规划中，未实现）

排程机制（`EventManager.ScheduleEvents`，结合回合规则：默认三选一至少 1 商店 / 第 4 回合三怪物 / 第 8 回合 PvP）：
- 按当前轮过滤可排布事件模板：`MinRound <= 当前轮 <= MaxRound`。
- **未出现加成**：事件有基础权重（`Weight`，默认 1）；本局尚未出现的事件权重乘固定倍率（`UNSEEN_MULTIPLIER` 常量，可调）提高排布几率，已出现事件回落基础权重。
- `EventManager` 按 `EventKey` 跟踪本局已出现事件，进入 `InMatch`（新对局）时清空。

### 英雄系统

- **继承定义**：每个英雄是 `HeroBase` 子类（放 `scripts/Entities/Heroes/`），构造函数内创建 `HeroAttributeSet`（注入 `HeroKey`/`HeroDisplayName`/`FactionKey`）并覆写 `ApplyInitialAttributes()` 实现初始属性差异；**不建英雄场景**（逻辑层纯代码）。
- **模板池**：`HeroManager` 用**反射扫描程序集**，`_Ready` 自动收集所有非抽象 `HeroBase` 子类各建一个作模板（`AvailableHeroes`）；玩家选择时从模板 `Duplicate()` 复制独立实例（含独立 `AttributeSet`）成为 `CurrentHero`。
- **商店与卡牌**：英雄不管理商店。卡牌定义携带**归属 key**（`HeroKey`）；商店由 `ShopEvent` 事件触发，`CardManager` 按归属 key 过滤卡池。

### 卡牌系统

- **继承定义**：卡牌是 `CardBase` 抽象基类的子类（放 `scripts/Entities/Cards/`），构造函数内创建 `CardAttributeSet`（注入 `CardKey`/`DisplayName`/`HeroKey`/`Size`）。功能相似的卡牌后期可继承自同一中间抽象类。
- **身份字段不可变**：`CardKey` / `DisplayName` / `HeroKey` / `Size` 为 get-only，创建后不可修改；`Level` / `Value` / `Cooldown` 走 `AriaAttributeData`（可变，数值流转）。
- **价值公式**：初始价值 = **价值系数 × 等级 × 型号系数**（小 1 / 中 2 / 大 3）。普通卡系数 2（`CardEconomy.STANDARD_VALUE_SCALE`），**特殊价值物品**在子类覆写 `ValueScale`（如兽皮系数 4）。`CardBase.GetInitialValue()` 实时推导；构造函数末尾经 `InitializeValue()` 把现值同步进 `Value` 属性（等级变化**不**自动重算价值，增值/减值事件直接修改 `Value`）。
- **获得折半**：**任何途径**加入玩家卡池即视为获得（购买、掉落、奖励一致），价值在 `CardManager.AddCardToPlayer` 统一按 `初始价值 × CardEconomy.OBTAINED_VALUE_RATIO(0.5)` 折半；`BuyCard` 按初始价值全额计价。折半与购买行为无关，目的是让后续增值/减值事件的**叠加逻辑**始终作用在一致的半价基线上；`SellCard` 按现值 `Value` **全额**回补，不再二次打折。
- **Key 类型统一用 `StringName`**：所有标识性 key（`CardKey` / `HeroKey`）一律用 `Godot.StringName`（创建用 `new StringName("...")`），`DisplayName`/`HeroDisplayName` 等展示文本保持 `string`。
- **Key 与展示名分离**：key（标识）与 display name（展示）使用不同字段，不用同一个字段兼任（如英雄 `HeroKey` + `HeroDisplayName`、卡牌 `CardKey` + `DisplayName`）。
- **身份字段归属**：身份字段（key / display name / 归属）一律放进实体对应的 `*AttributeSet`（`HeroAttributeSet` / `CardAttributeSet`），不放实体 Node 上。
- **模板池**：`CardManager` 同样用**反射**收集所有非抽象 `CardBase` 子类作模板（`CardTemplates`）；`CreateCard` 复制实例、`AddCardToPlayer` 记录玩家拥有卡牌、`GetCardsByFaction(factionKey)` 供商店过滤。

### 标签（词条）系统

- **标签容器**：`addons/aria/tags/AriaTagSet`（`HashSet<StringName>`），轻量级通用标签容器，无元数据，只存 key。
- **标签常量**：`scripts/Core/Types/Tags.cs` 定义所有游戏标签（`Small`/`Medium`/`Large`/`Weapon`/`Clothing`/`Human`/`Mechanical`/`Vehicle`/`Consumable`）。
- **显示名映射**：`Tags.GetDisplayName(StringName tag)` 返回中文显示名（如 `Weapon` → `"武器"`），未知标签返回原始 key。
- **标签由卡牌构造函数挂载**：如 `TagSet.Add(Tags.FromSize(AttributeSet.Size))` + `TagSet.Add(Tags.Weapon)`。

### 棋盘与推挤系统（已建）

**分层**：非 UI 层三个模块均不依赖 UI，只依赖卡牌数据类（`CardBase`/`CardAttributeSet`）与尺寸枚举（`CardSize`）：

- `scripts/Board/Data/GameBoard`：棋盘数据管理（纯类）。**只存卡牌引用 + `Order`（左→右顺序）+ `StartCell`（占用格）**，不存渲染坐标；容量可配（默认 10）；提供放置/移除/交换/`order` 归一与 `CardPlaced/Removed/Swapped/LayoutChangedEvent`。
- `scripts/Board/Algo/BoardUtil`：**纯算法**（静态类）。阻挡卡牌查找、向右/向左推挤逐格模拟、方向择优（距离短→影响卡牌少→取 Right）。
- `scripts/Board/Algo/PushEvaluator`：只读评估（边界校验、快照排除被拖卡牌以释放原位、调用 `BoardUtil`、组装 `PushPlan`）。
- `scripts/Board/Algo/PushExecutor`：执行 `PushPlan`（按 `ResultOffsets` 写回各 `StartCell`、落位、重排 order、发事件）。
- `scripts/Board/Algo/BoardState`（含 `PushDirection`）：`None/Left/Right`。
- `scripts/Board/Manager/BoardManager`：`[GlobalClass] Node`，持有**战场区 + 备战区**两个 `GameBoard`（各 10 格），编排跨区拖拽（同盘移动释放原位；跨盘先查来源盘再评估目标盘，即"查两个区域"）。

**交互 API（供 UI）**：`PreviewMove(card, targetBoard, targetCell)`（只读预览推挤方案）、`CommitMove(...)`（提交执行，不可行返回 false）、`RemoveCard`、`SwapCards`、`GetStartCell`、`GetLayout`（UI 据此换算像素）。

**推挤规则**：目标区间无阻挡→直接放置；有阻挡→任一块 `CanPush=false` 直接失败；否则分别模拟向右/向左，择优执行。

**扩展预留**：容量可配、逐卡 `CanPush` 标记、`PushPlan.ResultOffsets` 预留 undo/redo、等级/归属优先级钩子后续可加。

**测试 UI**：`test_ui/` 独立于核心（核心**不引用**它，仅它引用核心），在 Godot 中单独打开 `res://test_ui/BoardTestUI.tscn` 或 `BattleTestUI.tscn` 运行。`BattleTestUI` 已集成棋盘网格渲染：使用 `BoardManager` 管理战场，卡牌按尺寸渲染（Small 1格、Medium 2格、Large 3格，宽度比 Small 1:2 / Medium 1:1 / Large 3:2），点击卡牌列表选中 → 点棋盘空格放置；点已放卡牌 → 点空格移动；敌方棋盘自动放置、仅显示。卡牌 UI 从上到下依次显示：名称 → 归属（`HeroKey`）→ CD → 词条（标签） → 能力。为保证隔离，`CardManager` 反射模板收集过滤 `type.IsPublic`，排除 `test_ui` 内部测试卡。

### MVC 分离约定（英雄 UI）

- 英雄 UI 为**独立类**，与底层逻辑分离，分两类：`HeroSelectionUI`（选角界面）与 `InMatchHeroUI`（局内界面），放 `scripts/UI/`。
- UI 只读 Manager 状态（如 `HeroManager.AvailableHeroes` / `CurrentHero`）、订阅事件刷新，**不直接改模型**。
- 英雄视觉（立绘/模型）由 UI 层按英雄标识映射加载，英雄实体不感知自身表现。
- 英雄 UI 尚未实现，仅约定；实现时机以 UI 开发阶段为准。

### 能力系统（框架已建，战斗管理器待实现）

**核心模型：能力触发效果**

- **能力（Ability）**：可被触发的动作单元。主动（冷却/手动）与被动（事件触发）都是能力，区别仅在于触发方式，由管理器决定。能力类只负责**执行逻辑**。
- **效果（Effect）**：被能力触发的被动后果（静态修饰 / 周期 DoT-HoT，细节待细化）。
- 触发、冷却、编排、连锁（≤3 层上限）统一由 `Resolver`（确定性战斗解析器）负责；管理器只与实体（`ICombatant`）通信，不直接驱动能力。

**分层与文件**

- Aria 层（`addons/aria/`，游戏无关，保留 `Aria` 前缀）：
  - `interfaces/IAriaEntity.cs`：最小实体接口，暴露 `AriaAttributeSet`。
  - `contexts/AriaContextBase.cs`：上下文基类，供游戏侧继承自定义。
  - `abilities/AriaAbilityBase.cs`：能力基类（Key/DisplayName/冷却 + `CanActivate`/`Activate`，`Activate` 返回 `AriaAction[]`；`Owner` 属性由管理器注入，`GetCooldownSeconds()` 供子类覆写从属性集动态读取冷却）。
  - `abilities/AriaAction.cs`：动作载体（目标 → 效果字典，目标可多个）。
  - `abilities/AriaAbilityHandle.cs`：能力运行期句柄（冷却计时 + 所属实体）。
  - `effects/AriaEffectBase.cs`：效果基类（持续类型/时长/周期 + `Apply`/`Remove`/`Tick`/`GetTickEffects`）。
  - `effects/AriaEffectHandle.cs`：效果运行期句柄（挂载信息 + 计时，管理器统一驱动）。
  - `effects/AriaEffectDurationType.cs`：效果持续类型（Instant/HasDuration/Permanent）。
  - `attributes/AriaAttributeOperation.cs`：属性运算方式（Add/Subtract/Multiply/Override）。
  - `attributes/AriaAttributeModifier.cs`：属性修饰器（AttributeKey + Operation + Magnitude）。
  - `attributes/AriaAttributeSet.cs`：支持 `ApplyModifier`/`RemoveModifier`（记录原值回滚）。
- 游戏侧（不带 `Aria` 前缀）：
  - `scripts/Core/Interfaces/ICombatant.cs`：战斗参与者接口（英雄/卡牌共同实现），声明 `Abilities`/`Effects`。
  - `scripts/Core/Interfaces/IPassiveAbility.cs`：被动能力接口，声明响应的事件类型（`Type ReactEventType`）；主动能力不实现该接口。
  - `scripts/Core/Interfaces/IDamageEffect.cs`：伤害效果接口（结算器据此识别伤害并广播）。
  - `scripts/Core/Interfaces/IHealEffect.cs`：治疗效果接口（结算器据此触发净化：削减目标腐蚀/辐射）。
  - `scripts/Combat/Events/CombatEventBase.cs`：战斗事件抽象基类；子类 `BattleStartEvent`/`AbilityActivatedEvent`/`AdjacentCardActivatedEvent`/`EffectAppliedEvent`/`DamageDealtEvent`/`HealthBelowHalfEvent`/`NearDeathEvent` 自带载荷，被动按事件类型路由。
  - `scripts/Combat/Events/CombatEventBus.cs`：全局战斗事件总线（静态观察者通道，供 UI/统计/管理器订阅；被动连锁不走总线）。
  - `scripts/Combat/Contexts/BattleContext.cs`：战斗上下文（继承 `AriaContextBase`），含来源/目标/当前事件（`CurrentEvent`）、双方英雄（单个）与卡牌（数组）。

**能力子类写法**（游戏侧 `scripts/Entities/Abilities/`）：
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

**效果子类写法**（游戏侧 `scripts/Entities/Effects/`）：
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

**效果堆叠约定**（适用于所有可叠加属性：时长、腐蚀、辐射、生命再生等）：

- **Apply 累加**：效果的 `Apply()` 必须**累加**到目标属性（`CurrentValue + amount`），而非覆盖赋值。多个来源对同一目标施加同类效果时，属性自然叠加。
- **到期扣减**（HasDuration 效果）：`CombatManager` 在效果到期时，从目标属性中**扣减该实例贡献的量**（通过 `_durationContributions` 字典追踪），而非直接清零。Instant 效果无此问题，属性由衰减逻辑自然归零。
- **不清零**：HasDuration 效果的 `Remove()` 不再直接设置属性为 0。属性值由 `TickCardDurations()` 每帧自然衰减至 0。
- **抵消判定**：`GetCooldownMultiplier()` 读取属性值判断倍率（超频×2 / 麻痹×0.5 / 两者均>0时抵消×1），与效果实例数量无关，仅看属性最终值。

**写新效果时的检查清单**：
1. 效果类继承 `AriaEffectBase`
2. `Apply()` 中对目标属性做 `SetCurrentValue(CurrentValue + amount)` 累加
3. **不覆写** `Remove()`（或 `Remove()` 为空操作）
4. 若属性需衰减，在 `Resolver` 中增加递减逻辑（如 `TickCardDurations()` 或 `ApplyDot()`）
5. 若需根据属性值计算倍率，在 `GetCooldownMultiplier()` 或类似方法中读取属性值

详细设计见 `docs/AbilityAndCombatFramework.md`。

### 战斗层（已建）

- `scripts/Combat/BattleClock`：1/30 秒固定步长时钟，`Advance(float delta)` 推进时间，每满一步触发 `StepTicked` 回调。
- `scripts/Combat/Resolver`（普通类）：确定性战斗解析器，包含全部战斗结算逻辑（冷却递减、主动能力发动、DoT/HoT、效果队列排水、死亡判定、被动连锁 ≤3 代）。每步调用 `Tick(float stepDelta)` 执行一步。
- `scripts/Combat/BattleSnapshot`：状态快照，每步保存所有参战实体的属性值，支持 `Rollback(int stepsBack)` 回滚。
- `scripts/Combat/Managers/CombatManager`（Node）：持有 `BattleClock` + `Resolver`，管理战斗生命周期 + A/B 超时（A=300s 正常 / B=600s PvP）+ 超时自动结束。
- 战斗步长由 `BattleClock.FIXED_STEP`（1/30s）驱动，`CombatManager._PhysicsProcess` 推进时钟，时钟每步回调 `Resolver.Tick`。

### 数据驱动模型（已建）

- `addons/aria/expressions/ValueExpression`：动态值表达式，支持常量、属性读取、算术运算，运算符 `+`-`*`/` 重载链式组合。用于能力/效果中需要动态计算的数值。
- `addons/aria/expressions/Condition`：条件判断，支持比较（Equals/GreaterThan/LessThan 等）、AND/OR/NOT 组合。用于能力激活条件、解锁条件等。
- `addons/aria/definitions/AbilityDefinition`：数据驱动能力定义（触发方式/目标选择/冷却/激活条件/效果列表）。不继承 `AriaAbilityBase`，而是持有配置数据。
- `addons/aria/definitions/EffectDefinition`：数据驱动效果定义（类型/数值表达式/持续时间/穿透标记）。
- `addons/aria/definitions/DataDrivenAbility`：桥接类，将 `AbilityDefinition` 包装为 `AriaAbilityBase`，可直接用于 Resolver。
- `addons/aria/definitions/EffectFactory`：从 `EffectDefinition` 创建 `AriaEffectBase` 实例的工厂。

### 池系统（已建）

- `scripts/Core/Pools/PoolBase<T>`：泛型实体池基类。反射收集模板、`Duplicate` + `DeepCopy AttributeSet` 创建实例、跟踪拥有/释放。消除各管理器中重复的反射模板收集逻辑。
- `scripts/Core/Bases/IEntity`：游戏实体公共接口，提供 `AttributeSet` 访问，供通用逻辑使用。

**能力复用约定**（重要）：

- ⚠️ **能力必须可复用，禁止为单张卡写专属能力**：能力是通用动作单元，一张卡由多个已有能力组合而成。
- **卡牌组合能力**：卡牌构造函数中通过 `Abilities.Add(...)` 组装已有能力（如 `AttackAbility` + `ParalysisAbility`），通过 `[Export]` 属性传入参数差异化。
- **新能力的判断标准**：只有当现有能力组合无法表达所需逻辑时，才新建能力类。新能力必须能被多张卡复用。
- **禁止的情况**：如 `ShockPistolAbility`（仅服务于"电击手枪"一张卡）→ 应拆分为 `AttackAbility` + `ParalysisAbility` 组合。
- **正确示例**：`电击手枪` = `AttackAbility` + `ParalysisAbility { DurationSeconds = 1f }`；`麻痹枪` = `ParalysisAbility { DurationSeconds = 3f }`；`重型攻击` = `AttackAbility { DamageAmount = 50f }`。
- **等级伤害由卡牌处理**：能力不感知等级。卡牌在构造函数中根据等级设置属性值（如 `ATTACK_POWER`），并在等级变化时通过 `OnLevelChanged` 钩子自动更新。能力只读属性，不关心值从哪来。

## 编码规范

### 命名规范

| 类型 | 规范 | 示例 |
|---|---|---|
| 类/结构体 | PascalCase | PlayerController, GameManager |
| 接口 | I + PascalCase | IDamageable, ISaveable |
| 方法 | PascalCase | TakeDamage(), SaveGame() |
| 本地变量 | camelCase | playerHealth, moveSpeed |
| 私有字段 | _camelCase | _health, _isInitialized |
| 公共属性 | PascalCase | Health, MaxSpeed |
| 常量 | UPPER_SNAKE_CASE | MAX_PLAYERS, DEFAULT_SPEED |
| 枚举 | PascalCase | GameState, WeaponType |
| 信号/事件 | Event 后缀 | HealthChangedEvent, GameOverEvent |
| 基类/抽象类 | Base 后缀 | AriaAbilityBase, CardBase, CombatEventBase |

- **基类 / 抽象类一律以 `Base` 结尾**：凡被继承的类（含抽象基类）命名必须带 `Base` 后缀，子类直接继承该命名，不允许裸名基类（如事件基类须为 `CombatEventBase`）。

### 代码规范

- 遵循 C# / Godot 官方风格，按上方命名规范执行。
- 每个类一个文件，文件名与类名一致（如 `PlayerController.cs` → `public class PlayerController`）。
- 类与脚本文件名保持一致（Godot 要求类名匹配文件名）。
- 通过节点路径引用时使用 `GetNode<T>("...")`；优先使用 `@export` 在 Inspector 暴露参数。
- 不使用 `_Process` 计算固定逻辑，改用 `_PhysicsProcess`。
- 注释一律使用普通 `//` 注释，**不使用 XML 文档注释（`///`）**。
- **所有公共类型**（类 / 接口 / 结构体 / 枚举 / 公共属性 / 公共字段 / 公共常量）必须加注释，说明其用途。
- **重要的公共函数 / 变量**应加注释（非全部），说明其行为、参数、返回值或约束；简单的、自解释的公共成员可不加。
- **重载成员不加注释**：重载的函数 / 变量（如多个构造函数、同名方法）不逐一注释，意图由首个声明或上下文表达。
- **叶子类注释从简**：继承/依赖链越末端的具体类（如实体子类、枚举、简单工具类）注释越少，仅保留类型级或分组级注释，不为琐碎成员逐一加注。
- 提交前不包含任何密钥或敏感信息。

### 性能注意事项

- 避免在 `_Process()` 中使用 `GetNode()` 或 `FindChild()`，应在 `_Ready()` 中缓存节点引用。
- 使用 Pool（对象池）管理频繁创建/销毁的对象（如子弹）。
- 在 `_ExitTree()` 中断开信号连接，防止内存泄漏。
- 使用 `[GodotClass]` 属性标记自定义类。

## Git 工作流

- **main**：主分支（生产环境）
- **dev**：开发分支（日常开发）
- **feature/功能名称**：功能分支
- **hotfix/问题描述**：修复分支
- **默认推送 dev**：日常提交/推送仅操作 `dev` 分支，**禁止推送 main**（除非用户明确要求）。
- **默认不加代理**：拉取/推送默认直连（git 不配置 http/https proxy）。
- **拉取失败用代理重试**：直连失败时，临时用代理 `http://127.0.0.1:7897` 重试（仅本次命令生效，不写入配置）：
  `git -c http.proxy=http://127.0.0.1:7897 -c https.proxy=http://127.0.0.1:7897 pull origin dev`

### 提交规范

格式：`<type>(<scope>): <subject>`

- `feat: 添加玩家冲刺功能`
- `fix: 修复碰撞检测问题`
- `docs: 更新 API 文档`

### 其他约定

- 修改场景（.tscn）时注意保留 `.uid`，避免破坏资源引用。
- **新建文件按用途归类，禁止乱扔进 `Bases/`**：接口→`Interfaces/`（或 Aria `interfaces/`）、上下文→`Contexts/`（或 Aria `contexts/`）、枚举/类型→`Types/`、实体基类才进 `Bases/`、实体子类进 `Entities/`。
- **按子系统分目录**：元游戏管理器→`Systems/`、棋盘实现→`Board/`（Manager/Data/Algo）、对局管理与事件→`Match/`、战斗实现→`Combat/`；可能被其他模块引用的共享类型放 `scripts/Core/`。

## 重要约束

- ❌ 不要直接修改 `.godot/` 文件夹内容
- ❌ 不要提交 `export_presets.cfg` 到仓库（除非需要）
- ❌ 不要在场景中硬编码文件路径，使用 `[Export]` 或资源引用
- ✅ .NET SDK 版本必须 ≥ 6.0
- ✅ 确保 `.gitignore` 包含 `bin/` 和 `obj/` 目录
