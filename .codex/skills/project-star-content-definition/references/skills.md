# 技能

## 必读位置

- 正式数据：`docs/design/SKILL_DATA.md`
- 玩法语义：`docs/design/GAME_DESIGN.md`
- 能力系统：`docs/engineering/ARCHITECTURE.md`
- 代码范式：`scripts/Content/Skills/`

## 定义检查

- 明确 SkillKey、中文名、归属、初始等级、支持等级、触发条件和逐级数值。
- 技能不占棋盘格，首版只配置被动能力；不要使用主动能力或依赖卡牌自身位置/尺寸的效果。
- 使用 `SkillDefinition` 和逐级 `SkillLevelDefinition`，能力 key 使用 `ability.` 前缀。
- 触发应选择已有的 `AbilityActivation`。若缺少触发类型，新增通用事件语义并验证回响不会递归触发回响。
- 效果优先组合已有通用效果；技能来源必须能通过战斗快照独立结算。
- 新技能应生成 `scripts/Content/Skills/<Name>SkillDefinition.cs` 及对应 `.cs.uid`，并更新 `SKILL_DATA.md`。
- 验证至少覆盖逐级数值、正确触发次数、目标和最终结果。
