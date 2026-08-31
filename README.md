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
- **HeroManager**：英雄选角、持有 HeroBase、属性存取
- **CardManager**：卡牌数据库、实例化、构筑
- **BoardManager**：棋盘（战场/备战区排列、放置校验）
- **EventManager**：事件生成、分发、结算，持有两个子管理器（MonsterEventManager / ShopEventManager）

玩法框架使用自研 **Aria** 插件（参考 Forge for Godot 设计思路，不引用其运行时）。Aria 为跨游戏复用的通用插件，不含本项目专属逻辑。

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
│   ├── Core/          # 核心系统（管理器：Game/RoundTurn/Hero/Card/Board/Event 及子管理器；实体基类：HeroBase/CardBase/EnemyBase 等）
│   ├── Systems/       # 游戏系统（战斗、库存、存档）
│   ├── UI/            # UI控制器
│   └── Utils/         # 工具类（扩展方法、辅助函数）
├── shaders/           # 着色器 (.gdshader)
├── tests/             # 单元测试
├── Project_Star.csproj # C# 项目文件
└── Project_Star.sln   # 解决方案文件
```