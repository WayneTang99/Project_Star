# Project_Star

一个类《The Bazaar》（大巴扎）的卡牌异步对战自走棋项目，基于 Godot 4.7 开发。

## 玩法概述

- **英雄**：玩家选择角色进入游戏，携带属性集，是构筑与战斗的主体。
- **卡牌**：构成角色构筑与战斗手段的核心资源，可通过商店交易获得。
- **遭遇**：玩家回合中的选项（商店/怪物战/PvP），可能为单遭遇或多选一。
- **异步对战**：采用异步 PvP，无需双方同时在线。
- **自走棋**：战斗自动进行，玩家负责构筑与部署。

## 游戏流程

- 玩家**选择英雄**进入游戏。
- 游戏以**轮（Round）** + **回合（Turn）** 逐步递进，每回合可能出现各种遭遇。
- **商店遭遇**中，玩家可以**交易卡牌**。
- 玩家拥有**备战区（Bench）** 与 **战场区（Battlefield）**，可随意放置卡牌。
- **默认回合**为**三选一遭遇组**，其中**至少 1 个商店遭遇**。
- **每轮第 4 回合**：3 个怪物遭遇，三选一挑一个进入战斗。
- **8 回合为 1 轮**，每轮**最后一回合（第 8 回合）固定为异步玩家对战遭遇**。
- 玩家对战遭遇与怪物遭遇点击后**进入战斗**，战斗使用**战场区卡牌**自动进行。

## 开发入口

- Playtest.tscn：默认试玩入口。
- Main.tscn：规则分类验证，支持 headless --verify。
- scripts/Presentation/CardFace/CardFaceShowcase.tscn：三尺寸卡面展示。
- scripts/Presentation/Playtest/ComponentShowcase.tscn：组件与全卡池展示。

构建：`dotnet build Project_Star.csproj`。文档职责与规则入口统一见 [AGENTS.md](AGENTS.md)；当前工作见 [实施计划](docs/planning/IMPLEMENTATION_PLAN.md)，验证步骤见 [验收清单](docs/quality/MANUAL_ACCEPTANCE.md)。

源码在 scripts/ 五层目录；运行资源在 art/；本地日志与草稿在忽略的 output/；参考区仅供设计时人工查看，不作为项目依赖。
