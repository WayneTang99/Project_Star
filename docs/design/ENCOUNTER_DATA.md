# Project_Star 遭遇数据表

本文档记录正式遭遇内容数据，供设计、实现和审查使用。遭遇排程规则以 `docs/design/GAME_DESIGN.md` 为准；代码中的 `EncounterDefinition`、`MonsterDefinition` 和排程器是运行时数据来源。

## 1. 字段约定

| 字段 | 含义 |
|---|---|
| EncounterKey | 遭遇唯一 `StringName` key；怪物战使用对应怪物 key |
| 类型 | 商店 / 怪物战 / PvP / 可选项 |
| 轮次 | 可参与排程的轮次范围，端点包含在内 |
| 基础权重 | 在同类型候选池中的抽取权重；本局未出现过的遭遇按排程规则乘2 |
| 等级 | 商店自身等级、可选项遭遇等级或怪物等级；不代表商品卡牌的生成等级 |
| 配置 | 商店商品池、战斗对手或可选项效果 |

`—` 表示该类型没有对应等级。第4回合只抽怪物战，第8回合固定 PvP；其他回合从商店和可选项中生成候选，并保底至少一个商店。

## 2. 遭遇总表

| EncounterKey | 名称 | 类型 | 轮次 | 基础权重 | 等级 | 配置 |
|---|---|---|---|---:|---:|---|
| `encounter.shop.small` | 小型商店 | 商店 | 1～99 | 1 | 1 | 只提供当前英雄归属的小型卡牌 |
| `encounter.shop.medium` | 中型商店 | 商店 | 1～99 | 1 | 1 | 只提供当前英雄归属的中型卡牌 |
| `encounter.shop.large` | 大型商店 | 商店 | 1～99 | 1 | 2 | 只提供当前英雄归属的大型卡牌 |
| `encounter.physical_training` | 体能训练 | 可选项 | 1～99 | 1 | 1 | 固定展示两个选项，见下表 |
| `encounter.landfill` | 垃圾填埋场 | 可选项 | 1～99 | 1 | 1 | 固定展示金币选项，另随机展示一个卡牌选项，见下表 |
| `monster.boar` | 野猪 | 怪物战 | 1～99 | 1 | 1 | 第4回合候选；默认战斗属性，战场第1、2格各放一张独立的1级兽皮，第3～4格放一张1级中型野猪卡；携带1级冲撞技能 |
| `encounter.pvp` | 异步对战 | PvP | 1～99 | 1 | — | 每轮第8回合固定出现 |

野猪行由 `MonsterDefinition` 在排程时生成，不存在单独的 `EncounterDefinition` 内容类。怪物数量不足3个时，正式排程无法生成第4回合的三个不同候选；试玩模式允许展示当前已有怪物。

怪物战后按损失生命比例结算金币和经验：野猪满额分别为3金币、2经验。胜利时从其两张兽皮、一张野猪卡和一项冲撞技能中按实例等概率抽取一件待领取奖励，因此兽皮、野猪卡和冲撞的抽取机会为2:1:1。

## 3. 可选项数据

| EncounterKey | 选项 Key | 展示名 | 展示规则 | 效果 |
|---|---|---|---|---|
| `encounter.physical_training` | `encounter.physical_training.max_health` | 强化体魄 | 固定 | 永久增加当前英雄等级 ×10 的最大生命 |
| `encounter.physical_training` | `encounter.physical_training.health_regen` | 耐力训练 | 固定 | 永久增加当前英雄等级 ×1 的生命再生 |
| `encounter.landfill` | `encounter.landfill.wealth` | 获得2金币 | 固定 | 获得2金币 |
| `encounter.landfill` | `encounter.landfill.faction_small_card` | 获得本职业随机一张1级小型卡牌 | 随机槽权重60 | 自动获得当前英雄归属的随机1级小型卡牌 |
| `encounter.landfill` | `encounter.landfill.material_small_card` | 获得随机一张1级小型材料卡牌 | 随机槽权重40 | 自动获得随机1级小型材料卡牌 |

垃圾填埋场每次共展示两个选项；卡牌奖励优先进入战场区，其次进入备战区，两区均无法容纳时跳过卡牌生成，选项仍正常结算。

## 4. 内容维护约定

1. EncounterKey 与选项 Key 全局唯一，使用 `StringName`；怪物战的 EncounterKey 对应已注册的怪物 key。
2. 明确类型、轮次、基础权重和各展示槽；随机选项写明同一槽内的权重。
3. 商店等级与商品卡牌等级分开记录；怪物卡组和战斗属性以 `MonsterDefinition` 为准。
4. 新增或修改正式遭遇时同步更新本表，并核对定义注册、排程和选项结算。
