# Project_Star 英雄数据表

本文档说明玩家可选英雄数据的字段和维护规则。英雄数据行以 `docs/design/HeroDataTable.csv` 为准。英雄规则以 `docs/design/GAME_DESIGN.md` 为准，术语以 `docs/design/GLOSSARY.md` 为准；代码中的 `HeroDefinition` 是运行时数据来源。

## 1. 字段约定

| 字段 | 含义 |
|---|---|
| HeroKey | 英雄唯一 `StringName` key |
| 归属 | 卡牌和技能内容筛选使用的阵营 key；不限制跨阵营内容的使用 |
| 初始等级 | 创建英雄实例时的等级 |
| 收入 | 每轮开始时结算的基础金钱收入 |
| 战斗初值 | 每场战斗开始时由英雄基础属性生成的状态；对局内永久加成可改变实际值 |

英雄不使用卡牌标签。金钱和声望属于对局资源，不是英雄定义的身份字段或战斗初值。

## 2. 内容维护约定

1. HeroKey 和归属 key 使用 `StringName`；HeroKey 全局唯一，归属 key 与卡牌、技能的归属保持一致。
2. 只记录玩家可选的 `HeroDefinition`；怪物使用独立的 `MonsterDefinition`，不列为英雄。
3. 新增或修改正式英雄时同步更新 `HeroDataTable.csv`，并核对英雄定义、内容归属和验证入口。
