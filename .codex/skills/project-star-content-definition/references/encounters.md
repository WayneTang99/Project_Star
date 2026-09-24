# 遭遇

## 必读位置

- 正式数据：`docs/design/ENCOUNTER_DATA.md`
- 排程与结算规则：`docs/design/GAME_DESIGN.md`
- 代码范式：`scripts/Content/Encounters/`
- 通用定义与结算：`scripts/Domain/Definitions/`、`scripts/Application/Encounters/`

## 定义检查

- 明确 EncounterKey、中文名、类型、轮次范围、基础权重和遭遇等级。
- 商店等级不等于商品卡牌等级；商品池应通过通用筛选条件表达。
- 可选项的 option key 在本遭遇中唯一；每个选项恰好属于一个展示槽。
- 随机槽明确候选权重。奖励无法创建时的行为应遵循已有自动获得与待领取规则。
- 优先复用现有 `EncounterOptionEffectDefinition`。新效果必须是可复用的结算动作，不能在服务中按 EncounterKey 分支。
- 新遭遇应生成 `scripts/Content/Encounters/<Name>EncounterDefinition.cs` 及对应 `.cs.uid`，并更新 `ENCOUNTER_DATA.md`。
- 验证定义字段、排程资格、展示槽/权重和每种选项的状态变化或失败行为。
