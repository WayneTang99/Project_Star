# AGENTS.md

Project_Star 的代理工作指南。本文件供 AI 代理 / 开发者了解项目约定，请遵守其中的规则。

## 项目概览

- 基于 **Godot 4.7** 的 3D 游戏项目
- 使用 **C# / .NET** 编写脚本（dotnet 模块，程序集名 `Project_Star`）
- 物理引擎：**Jolt Physics**
- 渲染：**Forward Plus**，Windows 下使用 **D3D12** 驱动
- 视口拉伸模式：`canvas_items` + `expand` 自适应

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

- 新脚本放 `scripts/` 对应模块目录；新场景放 `scenes/`，命名与对应脚本保持一致。
- `.godot/` 由编辑器生成，禁止手动修改，已加入 `.gitignore`。

## 构建与运行

```powershell
dotnet build                    # 编译 C# 脚本
# 运行：用 Godot 4.7 (mono 版) 打开项目目录，或：
godot --path .                  # 编辑器可执行文件已配置好 mono 模块
```

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
- 只有被明确要求时才添加注释，代码本身应自解释。
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
- ✅ 所有公共 API 必须有 XML 文档注释