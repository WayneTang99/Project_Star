# 怪物与英雄

## 怪物

- 正式数据记录在 `docs/design/EncounterDataTable.csv`；字段规则见 `docs/design/ENCOUNTER_DATA.md`；代码范式位于 `scripts/Content/Monsters/`。
- 明确 MonsterKey、中文名、等级、战斗属性、卡牌实例、技能实例、棋盘起始格和战后奖励参数。
- 怪物复用玩家侧的卡牌、技能和战斗逻辑。不要创建怪物专属战斗分支。
- 所引用的卡牌/技能 key 与等级必须存在；卡牌布局不得越界或重叠。
- 怪物战由 `MonsterDefinition` 进入排程，不额外创建同名 `EncounterDefinition`。
- 新怪物生成 `scripts/Content/Monsters/<Name>MonsterDefinition.cs` 及对应 `.cs.uid`，同步更新 `EncounterDataTable.csv`；字段或规则变化时更新 `ENCOUNTER_DATA.md`。
- 验证注册表跨引用、棋盘布局、战斗快照、排程资格和奖励池。

## 英雄

- 正式数据记录在 `docs/design/HeroDataTable.csv`；字段规则见 `docs/design/HERO_DATA.md`；代码范式位于 `scripts/Content/Heroes/`。
- 明确 HeroKey、中文名、归属 key、初始等级、收入和全部战斗初值。
- 英雄不使用卡牌标签；金钱、经验和声望等对局资源不要误放进英雄身份或战斗初值。
- 新归属会影响卡牌、技能和商店的跨引用筛选，必须验证相关内容 key。
- 新英雄生成 `scripts/Content/Heroes/<Name>HeroDefinition.cs` 及对应 `.cs.uid`，同步更新 `HeroDataTable.csv`；字段或规则变化时更新 `HERO_DATA.md`。
- 验证身份、默认资源、战斗初值、定义注册和新对局创建。
