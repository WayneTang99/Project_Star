# Project_Star

一个类《The Bazaar》（大巴扎）的卡牌异步对战自走棋项目，基于 Godot 4.7 开发。

## 玩法概述

- **卡牌**：以卡牌为核心，卡牌构成角色构筑与战斗手段。
- **异步对战**：采用异步 PvP，无需双方同时在线。
- **自走棋**：战斗自动进行，玩家负责构筑与部署。

## 目录结构

```
Project_Star/
├── .godot/            # Godot 缓存（忽略）
├── assets/            # 资源文件
│   ├── sprites/       # 精灵图 (.png, .svg)
│   ├── sounds/        # 音效 (.wav, .ogg)
│   ├── models/        # 3D模型 (.glb, .fbx)
│   └── fonts/         # 字体 (.ttf)
├── scenes/            # 场景文件 (.tscn)
│   ├── Main.tscn      # 主场景
│   ├── Menu.tscn      # 菜单场景
│   └── Gameplay/      # 游戏玩法场景
├── scripts/           # C# 脚本 (.cs)
│   ├── Core/          # 核心系统（单例、管理器）
│   ├── Entities/      # 实体类（Player, Enemy, Item）
│   ├── Systems/       # 游戏系统（战斗、库存、存档）
│   ├── UI/            # UI控制器
│   └── Utils/         # 工具类（扩展方法、辅助函数）
├── shaders/           # 着色器 (.gdshader)
├── tests/             # 单元测试
├── Project_Star.csproj # C# 项目文件
└── Project_Star.sln   # 解决方案文件
```