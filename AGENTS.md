# AGENTS.md

Project_Star 的代理工作指南。本文件供 AI 代理 / 开发者了解项目约定，请遵守其中的规则。

## 项目概览

- 基于 **Godot 4.7** 的 3D 游戏项目
- 使用 **C# / .NET** 编写脚本（dotnet 模块，程序集名 `Project_Star`）
- 物理引擎：**Jolt Physics**
- 渲染：**Forward Plus**，Windows 下使用 **D3D12** 驱动
- 视口拉伸模式：`canvas_items` + `expand` 自适应

## 目录结构约定

```
src/          # C# 脚本源码（*.cs）统一放在这里
scenes/       # 场景文件（*.tscn）
resource/     # 资源（图片、音频、数据等），image 等子目录按类型细分
```

- 新脚本放 `src/`，按功能模块建立子目录（如 `src/player/`、`src/enemy/`）。
- 新场景放 `scenes/`，场景命名与对应脚本保持一致。
- `.godot/` 由编辑器生成，禁止手动修改，已加入 `.gitignore`。

## 构建与运行

```powershell
dotnet build                    # 编译 C# 脚本
# 运行：用 Godot 4.7 (mono 版) 打开项目目录，或：
godot --path .                  # 编辑器可执行文件已配置好 mono 模块
```

## 代码规范

- 遵循 C# / Godot 官方风格：类型、方法使用 `PascalCase`，局部变量 `camelCase`，`_` 前缀私有字段（如 `_speed`）。
- 类与脚本文件名保持一致（Godot 要求类名匹配文件名）。
- 通过节点路径引用时使用 `GetNode<T>("...")`；优先使用 `@export` 在 Inspector 暴露参数。
- 不使用 `_Process` 计算固定逻辑，改用 `_PhysicsProcess`。
- 只有被明确要求时才添加注释，代码本身应自解释。
- 提交前不包含任何密钥或敏感信息。

## 常见约定

- 提交信息用中文或英文均可，风格简洁、以动词开头。
- 修改场景（.tscn）时注意保留 `.uid`，避免破坏资源引用。