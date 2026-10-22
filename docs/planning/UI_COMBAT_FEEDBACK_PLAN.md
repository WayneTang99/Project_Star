# 战斗状态表现工作入口

更新时间：2026-10-22。等级标识、状态与回放表现的现行约定只维护在 [UI_SYSTEM](../design/UI_SYSTEM.md)，战斗规则以 [GAME_DESIGN](../design/GAME_DESIGN.md) 为准，不在计划中重复定义。

先在异步 PvP 切片验证真实敌方阵容、冻结数值和暂停／倍速／跳过不改变结算；隐身、修复、召唤、多重与永久成长进入切片稳定阶段人工验证。整体视觉收口按 [UI 专项待办](UI_HTML_PARITY_TODO.md) 执行，项目顺序见 [当前实施计划](IMPLEMENTATION_PLAN.md)，实际证据见 [MANUAL_ACCEPTANCE](../quality/MANUAL_ACCEPTANCE.md)。历史迁移过程由 Git 保存。
