# 局内单元测试（In-Engine Testing）

本项目采用 **Chickensoft.GoDotTest 2.0.46** 作为局内测试运行器。原因如下：

- **gdUnit4Net 不采用**：gdUnit4Net 的 C# 支持在 Godot 4.7 上不可靠，且不积极维护；其运行时与 4.7 的 C# 集成存在兼容风险。
- **GoDotTest 与 Godot 4.7 版本严格对齐**：`Chickensoft.GoDotTest 2.0.46` 的 NuGet 依赖为 `GodotSharp 4.7.2`，与项目引擎版本一致，属于 Chickensoft 主动维护的测试框架。
- **配套 GodotTestDriver**：`Chickensoft.GodotTestDriver 3.1.81` 用于集成测试（模拟输入、等待帧、驱动 UI 等），本任务仅引入，暂未使用。

## 为什么必须在引擎内跑测试

游戏实体（`HeroBase` / `CardBase` 等）直接派生自 Godot 的 `Node` / `Resource`。脱离引擎的普通 xUnit 在构造这些节点时会崩溃，因此必须让 Godot 加载程序集后在引擎内运行测试。

## 目录与文件

```
tests/
├── TestRunner.cs      # 测试入口脚本：挂载在测试场景根节点，调用 GoTest.RunTests
├── TestRunner.tscn    # 测试场景（根节点为 Node，绑定 TestRunner.cs）
├── CanaryTest.cs      # 金丝雀测试：实例化真实引擎耦合实体（TemplateCard），验证引擎耦合与红灯/绿灯
└── Assert.cs          # 轻量断言辅助（GoDotTest 未捆绑断言库，抛异常即视为失败）
```

- 测试类继承 `Chickensoft.GoDotTest.TestClass`，用 `[Test]` 标记测试方法。
- 金丝雀测试在测试体内 `new TemplateCard()` 实例化一个派生自 Godot `Node` 的真实卡牌实体，证明引擎耦合在测试内可用。

## 运行命令（headless）

先编译（需先 restore）：

```powershell
dotnet build
```

用 Godot 4.7.2 mono 控制台版以 headless 模式运行测试（通过位置参数指定测试场景，覆盖主场景）：

```powershell
& "D:\Wayne\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . res://tests/TestRunner.tscn --quit-on-finish
```

- `--headless`：无窗口运行。
- `--path .`：指定项目根目录。
- `res://tests/TestRunner.tscn`：位置参数，直接运行测试场景而非 `run/main_scene`。
- `--quit-on-finish`：GoDotTest 测试跑完即退出。

测试结果会打印到控制台，包含通过的用例与失败详情（红灯时退出码非零）。

## 红灯 / 绿灯验证

- 让金丝雀刻意断言失败（如 `Assert.True(false, ...)`）→ 运行应报告 **RED**。
- 将金丝雀改为断言通过（`Assert.True(true, ...)`）→ 运行应报告 **GREEN**（0 失败）。

## ⚠️ 当前阻塞项（待修复）

在编写本测试基础设施时发现：**生产代码 `scripts/` 的工作区存在未提交的半成品改动，导致程序集无法编译**，因此上述 headless 运行命令暂无法成功执行。具体阻塞文件与报错：

- `scripts/Systems/GameManager.cs`：
  - 引用未定义的字段/常量 `MatchWinCount`、`VICTORY_WIN_THRESHOLD`。
  - 引用不存在的枚举成员 `MatchEndReason.Victory`（`GameState.cs` 中枚举仅含 `Defeat` / `Surrendered`）。
  - `RecordPvPLoss(HeroBase hero, int round)` 使用了 `HeroBase` 但缺少 `using Project_Star.Core.Bases;`。
- `scripts/Entities/Monsters/GoblinRaiderMonster.cs`：
  - `_Ready()` 中调用 `Deck.Add(...)`，但 `MonsterBase` 未定义 `Deck` 成员。

这些是本任务范围外的、开发者未提交的半成品功能代码，按约束不予以修改。**待上述生产代码修复后可编译**，再运行上述命令即可获得 RED / GREEN 证据。

## 说明

- 新增的 NuGet 包引用（`Chickensoft.GoDotTest` / `Chickensoft.GodotTestDriver`）已加入 `Project_Star.csproj`，可从本地 NuGet 缓存离线还原，无需网络。