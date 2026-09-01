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

所有管理器与实体基类均位于 `scripts/Core/` 下的 `Managers/` 与 `Bases/`（类型定义在 `Types/`）。管理器**不注册为 Autoload、不作为场景子节点**，由根节点脚本 `scripts/Core/Main.cs` 在 `_Ready` 中 **new 生成并 AddChild 挂载**、统一注册；`EventManager` 持有两个子管理器（普通类实例，生命周期随 `EventManager`）。

```
Main (主场景根节点，挂 Main.cs，_Ready 生成并缓存各管理器引用)
├── GameManager              # 主状态机：选英雄 → 局内 → 结算（信号广播状态切换；EndMatch/Surrender 强制结束总线）
├── RoundTurnManager         # 局内轮次：8回合/轮、每回合事件派发、末回合固定PvP、轮末声望失败判定
├── HeroManager              # 英雄：持有英雄模板池（每种各一个）、选择后复制实例、属性存取
├── CardManager              # 卡牌：反射收集模板池、实例化、玩家拥有卡牌、按阵营key过滤供商店
├── BoardManager             # 棋盘：战场/备战区双棋盘（各10格）、推挤放置/移除/交换/跨区拖拽编排
└── EventManager             # 事件：类型注册、生成（单/多选）、结果结算
    ├── MonsterEventManager   # 子管理器：怪物事件（生成怪物、点击后转 CombatManager 战斗）
    └── ShopEventManager      # 子管理器：商店事件（交易卡牌、扣 Wealth 属性）
```

**通信与数据约定**

- 管理器间通过 **Godot 信号（C# event）** 通信，避免互相直调。
- 数值流转一律走 `AriaAttributeSet`（扣财富、扣血等），不直接改字段。
- `CardManager` 管卡牌实例与数据，`BoardManager` 管卡牌在棋盘上的位置与布局。
- 实体基类（`HeroBase` / `CardBase` / `EnemyBase` 等）与管理器同放 `scripts/Core/`，供各系统与 UI 复用。

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
│   ├── aria/          # Aria 自研玩法插件（属性、能力等）
│   └── forge/         # Forge for Godot 插件（仅作参考，不引用）
├── scenes/            # 场景文件 (.tscn)
│   ├── Main.tscn      # 主场景
│   ├── Menu.tscn      # 菜单场景
│   └── Gameplay/      # 游戏玩法场景
├── scripts/           # C# 脚本 (.cs)
│   ├── Core/          # 核心系统
│   │   ├── Types/     # 类型定义（枚举等：GameState、CardState、HeroState、BoardState）
│   │   ├── Managers/  # 管理器（GameManager / RoundTurnManager / HeroManager / CardManager / BoardManager / EventManager 及子管理器）
│   │   ├── Bases/     # 实体基类（HeroBase / CardBase / EnemyBase 及对应属性集：HeroAttributeSet / CardAttributeSet）
│   │   ├── Board/     # 棋盘：数据管理（GameBoard/BoardEntry）、纯算法（BoardUtil）、推挤评估/执行（PushEvaluator/PushExecutor/PushPlan）
│   │   └── Main.cs    # 主场景根节点脚本（缓存各管理器引用）
│   ├── Entities/      # 实体子类（Heroes/TemplateHero、Cards/TemplateCard、Events/TemplateEvent 等）
│   ├── Systems/       # 游戏系统（战斗、库存、存档）
│   ├── UI/            # UI控制器（HeroSelectionUI / InMatchHeroUI 等）
│   └── Utils/         # 工具类（扩展方法、辅助函数）
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

- `scripts/Core/Bases/HeroAttributeSet`：英雄属性集（继承 `AriaAttributeSet`，含**不可变**身份字段 `HeroKey`/`HeroDisplayName`/`FactionKey` 与生命 / 护甲 / 财富 / 经验 / 等级 / 声望）。
- `scripts/Core/Bases/HeroBase`：英雄基类（Node，持有 `HeroAttributeSet`，`ApplyInitialAttributes` 供子类覆写）。
- `scripts/Entities/Heroes/TemplateHero`：示例英雄（继承 `HeroBase`，覆写初始属性）。
- `scripts/Core/Managers/HeroManager`：英雄管理器（**反射**收集所有非抽象 `HeroBase` 子类作模板池、`SelectHero` 复制实例、`HeroSelectedEvent`/`HeroResetEvent`）。
- `scripts/Core/Bases/CardAttributeSet`：卡牌属性集（继承 `AriaAttributeSet`，含**不可变**身份字段 `CardKey`/`DisplayName`/`FactionKey`/`Size` 与可变 `Level`）。
- `scripts/Core/Bases/CardBase`：卡牌抽象基类（Node，持有 `CardAttributeSet`）。
- `scripts/Entities/Cards/TemplateCard`：示例卡牌（继承 `CardBase`）。
- `scripts/Core/Managers/CardManager`：卡牌管理器（**反射**收集模板池、`CreateCard` 复制实例、`AddCardToPlayer`、`GetCardsByFaction` 供商店过滤）。
- `scripts/Core/Types/GameState` + `scripts/Core/Managers/GameManager`：主状态机（`StateChangedEvent` 信号广播，`EndMatch`/`Surrender` 强制结束总线）。
- `scripts/Core/Types/GameState`（含 `MatchEndReason`）+ `scripts/Core/Managers/RoundTurnManager`：局内轮次（8 回合/轮、末回合 PvP、轮末声望失败判定）。
- `scripts/Core/Types/EventState` + `scripts/Core/Bases/EventAttributeSet`：事件属性集（继承 `AriaAttributeSet`，含**不可变**身份字段 `EventKey`/`EventDisplayName`）。
- `scripts/Core/Bases/EventBase`：事件抽象基类（Node，持有 `EventAttributeSet`，`State` 生命周期，`Initialize`/`OnResolve` 供子类覆写）。
- `scripts/Core/Bases/ShopEventBase` + `scripts/Core/Bases/MonsterEventBase`：商店 / 怪物事件**中间态抽象类**。
- `scripts/Core/Managers/EventManager`：事件管理器（**反射**收集模板池、`ScheduleEvents` 排程钩子留空待排程系统覆写、`CreateEvent`/`AddEvent`/`ClearEvents`/`ResolveEvent`、`EventsGeneratedEvent`/`EventResolvedEvent`，持有 `MonsterEventManager`/`ShopEventManager` 空壳子管理器）。
- `scripts/Entities/Events/TemplateEvent` / `TemplateShopEvent` / `TemplateMonsterEvent`：示例事件（通用 / 商店 / 怪物）。
- `scenes/Main.tscn` + `scripts/Core/Main.cs`：主场景根节点，`_Ready` 中 new 生成并挂载各管理器。

### 英雄系统

- **继承定义**：每个英雄是 `HeroBase` 子类（放 `scripts/Entities/Heroes/`），构造函数内创建 `HeroAttributeSet`（注入 `HeroKey`/`HeroDisplayName`/`FactionKey`）并覆写 `ApplyInitialAttributes()` 实现初始属性差异；**不建英雄场景**（逻辑层纯代码）。
- **模板池**：`HeroManager` 用**反射扫描程序集**，`_Ready` 自动收集所有非抽象 `HeroBase` 子类各建一个作模板（`AvailableHeroes`）；玩家选择时从模板 `Duplicate()` 复制独立实例（含独立 `AttributeSet`）成为 `CurrentHero`。
- **商店与卡牌**：英雄不管理商店。卡牌定义携带**阵营 key**；商店由 `ShopEventManager` 与 `CardManager` 交互，按阵营 key 过滤卡池。

### 卡牌系统

- **继承定义**：卡牌是 `CardBase` 抽象基类的子类（放 `scripts/Entities/Cards/`），构造函数内创建 `CardAttributeSet`（注入 `CardKey`/`DisplayName`/`FactionKey`/`Size`）。功能相似的卡牌后期可继承自同一中间抽象类。
- **身份字段不可变**：`CardKey` / `DisplayName` / `FactionKey` / `Size` 为 get-only，创建后不可修改；`Level` 走 `AriaAttributeData`（可变，数值流转）。
- **Key 类型统一用 `StringName`**：所有标识性 key（`CardKey` / `FactionKey` / `HeroKey`）一律用 `Godot.StringName`（创建用 `new StringName("...")`），`DisplayName`/`HeroDisplayName` 等展示文本保持 `string`。
- **Key 与展示名分离**：key（标识）与 display name（展示）使用不同字段，不用同一个字段兼任（如英雄 `HeroKey` + `HeroDisplayName`、卡牌 `CardKey` + `DisplayName`）。
- **身份字段归属**：身份字段（key / display name / faction）一律放进实体对应的 `*AttributeSet`（`HeroAttributeSet` / `CardAttributeSet`），不放实体 Node 上。
- **模板池**：`CardManager` 同样用**反射**收集所有非抽象 `CardBase` 子类作模板（`CardTemplates`）；`CreateCard` 复制实例、`AddCardToPlayer` 记录玩家拥有卡牌、`GetCardsByFaction(factionKey)` 供商店过滤。

### 棋盘与推挤系统（已建）

**分层**：非 UI 层三个模块均不依赖 UI，只依赖卡牌数据类（`CardBase`/`CardAttributeSet`）与尺寸枚举（`CardSize`）：

- `scripts/Core/Board/GameBoard`：棋盘数据管理（纯类）。**只存卡牌引用 + `Order`（左→右顺序）+ `StartCell`（占用格）**，不存渲染坐标；容量可配（默认 10）；提供放置/移除/交换/`order` 归一与 `CardPlaced/Removed/Swapped/LayoutChangedEvent`。
- `scripts/Core/Board/BoardUtil`：**纯算法**（静态类）。阻挡卡牌查找、向右/向左推挤逐格模拟、方向择优（距离短→影响卡牌少→取 Right）。
- `scripts/Core/Board/PushEvaluator`：只读评估（边界校验、快照排除被拖卡牌以释放原位、调用 `BoardUtil`、组装 `PushPlan`）。
- `scripts/Core/Board/PushExecutor`：执行 `PushPlan`（按 `ResultOffsets` 写回各 `StartCell`、落位、重排 order、发事件）。
- `scripts/Core/Types/BoardState`（含 `PushDirection`）：`None/Left/Right`。
- `scripts/Core/Managers/BoardManager`：`[GlobalClass] Node`，持有**战场区 + 备战区**两个 `GameBoard`（各 10 格），编排跨区拖拽（同盘移动释放原位；跨盘先查来源盘再评估目标盘，即"查两个区域"）。

**交互 API（供 UI）**：`PreviewMove(card, targetBoard, targetCell)`（只读预览推挤方案）、`CommitMove(...)`（提交执行，不可行返回 false）、`RemoveCard`、`SwapCards`、`GetStartCell`、`GetLayout`（UI 据此换算像素）。

**推挤规则**：目标区间无阻挡→直接放置；有阻挡→任一块 `CanPush=false` 直接失败；否则分别模拟向右/向左，择优执行。

**扩展预留**：容量可配、逐卡 `CanPush` 标记、`PushPlan.ResultOffsets` 预留 undo/redo、等级/阵营优先级钩子后续可加。

**测试 UI**：`test_ui/` 独立于核心（核心**不引用**它，仅它引用核心），在 Godot 中单独打开 `res://test_ui/BoardTestUI.tscn` 运行，点击式演示放置/移动/推挤/不可推挤。为保证隔离，`CardManager` 反射模板收集过滤 `type.IsPublic`，排除 `test_ui` 内部测试卡。

### MVC 分离约定（英雄 UI）

- 英雄 UI 为**独立类**，与底层逻辑分离，分两类：`HeroSelectionUI`（选角界面）与 `InMatchHeroUI`（局内界面），放 `scripts/UI/`。
- UI 只读 Manager 状态（如 `HeroManager.AvailableHeroes` / `CurrentHero`）、订阅事件刷新，**不直接改模型**。
- 英雄视觉（立绘/模型）由 UI 层按英雄标识映射加载，英雄实体不感知自身表现。
- 英雄 UI 尚未实现，仅约定；实现时机以 UI 开发阶段为准。

### 能力系统（规划中，未实现）

- **通用能力**：能力可被**英雄与卡牌**共同使用，通过**继承**定义各种能力（如攻击、施法、被动）。
- **释放判断条件**：由每个能力子类**自定义**是否可释放（如冷却、资源、状态）。
- **触发方式**：提供**调用方法**供外部触发释放（管理器 / 事件 / 战斗逻辑调用）。
- **管理器**：后续建立能力管理器（授予、激活、更新）。
- 参考 Forge 的 Ability / AbilityData / AbilityHandle / IAbilityBehavior 结构，但按本项目简化、去 Tag 化、费用基于 `AriaAttributeData`。

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

## 重要约束

- ❌ 不要直接修改 `.godot/` 文件夹内容
- ❌ 不要提交 `export_presets.cfg` 到仓库（除非需要）
- ❌ 不要在场景中硬编码文件路径，使用 `[Export]` 或资源引用
- ✅ .NET SDK 版本必须 ≥ 6.0
- ✅ 确保 `.gitignore` 包含 `bin/` 和 `obj/` 目录
