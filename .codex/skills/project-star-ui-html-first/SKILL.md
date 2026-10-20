---
name: project-star-ui-html-first
description: Design or revise Project Star UI in the HTML prototype first, then synchronize the current prototype into the Godot project. Use for UI layout, card faces, detail panels, styling and interactions; excludes gameplay definitions and unrelated Git work.
---

# Project Star UI: HTML first

用户约定：UI 设计先改 HTML，再按当前 HTML 方案改项目。项目根目录为 D:/GodotSource/Project_Star。

1. 先读 AGENTS.md，按改动范围查 docs/design/UI_SYSTEM.md；原型入口为 docs/design/prototypes/game-16x9.html。查看现有工作区差异，保留用户及其他任务的修改。
2. 先在 HTML 实现本次设计，检查相关场景、布局和交互。当前原型是本次原生同步的视觉与交互基准，不把旧文档或旧组件样式重新覆盖到原型。
3. 用户明确“先改 HTML／只改 HTML”时止于原型并展示结果。用户已要求同步项目时，检查原型后继续同步，无需重复确认；有无法推断的设计歧义才提问。
4. 定位 scripts/Presentation/ 下现有组件及 Application 快照／查询。只做满足当前原型的改动；预览使用只读数据，不在 UI 改模型，不从 HTML 示例复制正式玩法数值。正式内容来源仍是 scripts/Content/。
5. 原生同步后构建并做与改动相称的验证，涉及布局时检查实际原生截图。说明已验证范围及剩余差异，不把浏览器通过说成原生通过；更新 UI_SYSTEM 的现行约定及相关验收记录。发现 bug 按 BUG_LOG 登记。

同步必须覆盖当前HTML的颜色、透明度、渐变方向与色阶、材质、内外描边、高光、阴影及厚度形成的3D感，以及正常、悬浮、键盘聚焦、按下、选中和禁用状态及过渡时长；不能只同步结构或用通用纯色主题替代。布局、字号、边距和选中标记占位在各状态保持稳定。验收同时检查静态原生截图与真实悬浮、点击、键盘操作，逐项记录视觉与交互差异。

用 rg 定位相关区域并按需读取，避免重复加载整份原型和无关文档。复用组件与语义配色；当前色值和行为以原型及 UI_SYSTEM 为准，技能中不重复维护。截图、日志放 output/，保留 .gdignore。提交与推送仅在用户请求时进行。
