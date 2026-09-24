# 卡牌与套装

## 必读位置

- 正式数据：`docs/design/CARD_DATA.md`（字段与规则）和 `docs/design/CardDataTable.csv`（数据行）
- 玩法语义：`docs/design/GAME_DESIGN.md`
- 架构与能力系统：`docs/engineering/ARCHITECTURE.md`
- 代码范式：`scripts/Content/Cards/`

## 卡牌定义检查

- 明确 CardKey、中文名、归属、尺寸、1～2 个元素属性、额外标签、初始等级和各支持等级。
- 尺寸标签由 `TagSet.ForCard` 自动生成，不要作为额外标签重复配置。
- 未显式指定价值时沿用通用尺寸/等级价值表；不要为默认值写冗余配置。
- 主动能力明确冷却、魔法消耗、目标和效果。被动能力必须选用与触发语义一致的 `AbilityActivation`。
- 需要攻击力、护甲等可成长数值时，将基值放入战斗属性并让能力读取属性，避免把同一数值分别硬编码在属性和效果中。
- 施加效果应累加；持续效果到期只扣除该实例贡献，不得直接清零目标属性。
- 新卡牌应生成 `scripts/Content/Cards/<Name>CardDefinition.cs` 及对应 `.cs.uid`，并更新 `CardDataTable.csv`；字段和维护规则有变化时同步更新 `CARD_DATA.md`。

## 卡牌套装

- 套装属于卡牌只读身份的可选归属，以战场区不同卡牌 key 计数。
- 多个已达阈值同时生效；复用通用能力，不在卡牌或战斗服务中写套装专属判断。
- 新套装同步检查定义注册的跨引用验证和战场快照行为。
