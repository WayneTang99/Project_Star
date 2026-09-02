# Project_Star

一个类《The Bazaar》（大巴扎）的卡牌异步对战自走棋项目，基于 Godot 4.7 开发。

## 玩法概述

- **英雄**：玩家选择角色进入游戏，携带属性集，是构筑与战斗的主体。
- **卡牌**：构成角色构筑与战斗手段的核心资源，可通过商店交易获得。
- **事件**：回合中出现的各种状况，可能为单事件或多选一。
- **异步对战**：采用异步 PvP，无需双方同时在线。
- **自走棋**：战斗自动进行，玩家负责构筑与部署。

## 游戏流程

- 玩家**选择英雄**进入游戏。
- 游戏以**轮（Round）** + **回合（Turn）** 逐步递进，每回合可能出现各种事件。
- **商店事件**中，玩家可以**交易卡牌**。
- 玩家拥有**备战区（Bench）** 与 **战场区（Battlefield）**，可随意放置卡牌。
- **8 回合为 1 轮**，每轮**最后一回合固定为异步玩家对战事件**。
- 玩家对战事件与怪物对战事件点击后**进入战斗**，战斗使用**战场区卡牌**自动进行。

## 管理器架构

所有管理器不注册为 Autoload、不作为场景子节点，由主场景 `scenes/Main.tscn` 的根节点脚本 `Main.cs` 在 `_Ready` 中 new 生成并 AddChild 挂载、统一注册；`EventManager` 持有两个子管理器（生命周期随主管理器）。

- **GameManager**：主状态机（选英雄 → 局内 → 结算），EndMatch/Surrender 强制结束总线
- **RoundTurnManager**：局内轮次（8 回合/轮、事件派发、末回合固定 PvP、轮末声望失败判定）
- **HeroManager**：反射收集英雄模板池（每种各一个）、选择后复制实例、属性存取
- **CardManager**：反射收集卡牌模板池、实例化、玩家拥有卡牌、按阵营 key 过滤供商店
- **BoardManager**：棋盘（战场/备战区双棋盘各 10 格、推挤放置/移除/交换/跨区拖拽编排）
- **EventManager**：事件生成、分发、结算，持有两个子管理器（MonsterEventManager / ShopEventManager）

玩法框架使用自研 **Aria** 插件（参考 Forge for Godot 设计思路，不引用其运行时）。Aria 为跨游戏复用的通用插件，不含本项目专属逻辑。

## 英雄系统

- **继承定义**：每个英雄是 `HeroBase` 子类，构造函数注入 `HeroAttributeSet`（`HeroKey`/`HeroDisplayName`/`FactionKey`）并覆写 `ApplyInitialAttributes()` 实现初始属性差异，逻辑层不建场景。
- **模板池**：`HeroManager` 用反射自动收集所有非抽象 `HeroBase` 子类各建一个模板，选择时 `Duplicate()` 复制独立实例。
- **商店**：卡牌定义带阵营 key，商店由 `ShopEventManager` 与 `CardManager` 按阵营 key 交互。
- **MVC**：英雄 UI（`HeroSelectionUI` / `InMatchHeroUI`）为独立类，只读 Manager、订阅事件，视觉由 UI 层加载。

## 卡牌系统

- **继承定义**：卡牌是 `CardBase` 抽象基类的子类，构造函数注入 `CardAttributeSet`（`CardKey`/`DisplayName`/`FactionKey`/`Size` 不可变，`Level` 可变）。
- **模板池**：`CardManager` 用反射自动收集所有非抽象 `CardBase` 子类作模板，`CreateCard` 复制实例、`GetCardsByFaction` 按阵营 key 过滤。

## 棋盘与推挤系统

- **数据/视图分离**：`GameBoard`（纯类）只存卡牌引用 + `Order` + `StartCell`，不存渲染坐标；位置由 UI 按 `GetLayout()` 换算像素。
- **双棋盘**：`BoardManager` 持战场区 + 备战区（各 10 格），支持同盘移动与跨区拖拽（先查来源盘再评估目标盘）。
- **推挤**：`BoardUtil` 纯算法模拟向右/向左逐格推挤（遇卡并入段），`PushEvaluator` 只读评估返回 `PushPlan`（可行/方向/距离/受影响卡牌/最终布局），`PushExecutor` 执行落位并重排 order。
- **择优**：目标被占时先释放被拖卡牌原位；方向选择按 距离短 → 影响卡牌少 → 取 Right。
- **扩展**：容量可配、逐卡 `CanPush` 标记（不可推挤直接失败）、`ResultOffsets` 预留 undo/redo。
- **测试 UI**：`test_ui/BoardTestUI.tscn` 独立于核心，在 Godot 中单独打开即可点击演示（选尺寸放卡 / 点卡移动 / 切换不可推挤）。

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
│   ├── Core/          # 共享定义/契约层（抽象基类、接口、非实体类型定义、Main）
│   ├── Systems/       # 元游戏管理器（GameManager / HeroManager / CardManager）
│   ├── Board/         # 棋盘子系统（Manager/Data/Algo）
│   ├── Match/         # 局内轮次与事件管理器（RoundTurnManager / EventManager）
│   ├── Combat/        # 战斗子系统（Managers/Contexts/Events/Effects）
│   ├── Entities/      # 实体子类（Heroes/TemplateHero、Cards/TemplateCard、Abilities、Effects 等）
│   └── UI/            # UI控制器（HeroSelectionUI / InMatchHeroUI 等）
├── shaders/           # 着色器 (.gdshader)
├── tests/             # 单元测试
├── test_ui/           # 独立测试UI（棋盘推挤演示，核心不引用）
├── Project_Star.csproj # C# 项目文件
└── Project_Star.sln   # 解决方案文件
```