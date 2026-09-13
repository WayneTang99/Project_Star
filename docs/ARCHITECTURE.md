# ARCHITECTURE.md
Project_Star 架构说明。本文件供 AI 代理 / 开发者理解项目架构，与 AGENTS.md 配合使用。

一、架构总原则
按生命周期分层：全局 / 对局 / 战斗，三层职责清晰。

战斗内外分离：两套事件总线，互不干扰。

数据与逻辑分离：Definition 静态、Instance 动态。

事件驱动：行为发事件，能力监听事件。

不按细节做架构：数值、条件、动作、触发器都是通用模型，新效果 = 新数据。

留 hook：事件类型、生成规则、结果、技能都可扩展。

继承优先：用继承结构复用，不堆管理器。

二、生命周期分层
层	生命周期	职责
全局	对局外常驻	主状态机、模板池、棋盘
对局	对局开始时创建，结束时销毁	轮次调度、事件生成执行、局外事件总线
战斗	对局开始时创建，常驻	战斗主循环、战斗内事件总线、快照回滚
text
Main
├── GameManager          : GlobalManagerBase
├── HeroManager          : GlobalManagerBase
├── CardManager          : GlobalManagerBase
├── BoardManager         : GlobalManagerBase
├── MatchManager         : MatchManagerBase
└── CombatManager        : MatchManagerBase
三、管理器架构
管理器列表
管理器	生命周期	职责
GameManager	全局	主状态机（选英雄 → 局内 → 结算）
HeroManager	全局	英雄模板池、实例化、玩家当前英雄
CardManager	全局	卡牌模板池、实例化、玩家拥有卡牌
BoardManager	全局	战场 / 备战双棋盘、推挤放置
MatchManager	对局	轮次调度 + 事件生成执行 + 局外事件总线
CombatManager	对局	战斗主循环 + 战斗内事件总线 + 快照回滚
管理器继承结构
text
ManagerBase（管理器基类）
├── 生命周期（Initialize / Dispose）
├── 事件订阅管理
└── 引用注入

├── GlobalManagerBase
│     ├── GameManager
│     ├── HeroManager
│     ├── CardManager
│     └── BoardManager
│
└── MatchManagerBase
├── MatchManager
└── CombatManager
普通类（非管理器）
类	归属	职责
RoundTurn	Match	轮次状态机
EventPool	Match	事件生成
EventExecutor	Match	事件执行
OutOfBattleEventBus	Match	局外事件总线
OutOfBattleTrigger	Match	局外触发器
BattleClock	Combat	固定步长时钟
InBattleEventBus	Combat	战斗内事件总线
BattleScheduler	Combat	能力调度 / CD / 连锁
BattleResolver	Combat	伤害 / 治疗 / 摧毁结算
BattleSnapshot	Combat	快照 / 回滚
BattleContext	Combat	战斗上下文
InBattleTrigger	Combat	战斗内触发器
四、整体结构图
graph TB
Main[Main.cs<br/>组合根]

    subgraph Global["全局管理器（对局外常驻）"]
        GM[GameManager]
        HM[HeroManager]
        CM[CardManager]
        BM[BoardManager]
    end

    subgraph Match["对局层（对局开始时创建）"]
        MM[MatchManager]
        RT[RoundTurn]
        EP[EventPool]
        EE[EventExecutor]
        OOB[OutOfBattleEventBus]
        OBT[OutOfBattleTrigger]
    end

    subgraph Combat["战斗层（对局开始时创建）"]
        CMB[CombatManager]
        BC[BattleClock]
        IEB[InBattleEventBus]
        BS[BattleScheduler]
        BR[BattleResolver]
        BSnap[BattleSnapshot]
        Ctx[BattleContext]
        IBT[InBattleTrigger]
    end

    subgraph Shared["共享层（无生命周期）"]
        Pools[Pools<br/>PoolBase T 子类]
        Trigger[TriggerSystem<br/>通用触发器]
        Aria[Aria 插件]
        Core[Core<br/>定义/接口/类型]
    end

    Main --> GM
    Main --> HM
    Main --> CM
    Main --> BM
    Main --> MM
    Main --> CMB

    MM --> RT
    MM --> EP
    MM --> EE
    MM --> OOB
    MM --> OBT

    CMB --> BC
    CMB --> IEB
    CMB --> BS
    CMB --> BR
    CMB --> BSnap
    CMB --> Ctx
    CMB --> IBT

    OBT --> Trigger
    IBT --> Trigger
    Trigger --> Aria
    Core --> Aria
    Pools --> Core

    style Global fill:#bbf,stroke:#333
    style Match fill:#bfb,stroke:#333
    style Combat fill:#fbf,stroke:#333
    style Shared fill:#fbb,stroke:#333
五、实体继承结构