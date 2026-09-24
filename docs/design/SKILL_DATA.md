# Project_Star 技能数据表

本文档说明正式技能数据的字段和维护规则。技能数据行以 `docs/design/SkillDataTable.csv` 为准。技能规则以 `docs/design/GAME_DESIGN.md` 为准，术语以 `docs/design/GLOSSARY.md` 为准；代码中的 `SkillDefinition` 是运行时数据来源。

## 1. 字段约定

| 字段 | 含义 |
|---|---|
| SkillKey | 技能唯一 `StringName` key |
| 归属 | 英雄阵营 key；`neutral` 表示无阵营 |
| 初始等级 | 默认获得时创建的等级 |
| 支持等级 | 定义中可创建的等级；普通等级最高合并至4级，5级只能直接获得 |
| 描述 | 技能的触发条件和各等级效果，数值按支持等级从低到高排列 |

技能独立于卡牌，不占战场区或备战区格位。首版技能只配置被动能力。`—` 表示没有该项。

## 2. 内容维护约定

1. SkillKey 全局唯一，使用 `StringName`；归属 key 对应已有英雄阵营或 `neutral`。
2. 初始等级必须存在对应的等级配置；描述按支持等级顺序列出数值。
3. 技能效果复用通用能力，不配置主动能力或要求卡牌自身作为来源的效果。
4. 新增或修改正式技能时同步更新 `SkillDataTable.csv`，并核对 `SkillDefinition` 和验证入口。
