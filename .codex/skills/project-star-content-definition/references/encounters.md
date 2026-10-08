# 遭遇

## 必读位置

- 正式数据：`scripts/Content/Encounters/` 中的 EncounterDefinition；字段与规则见 `docs/design/CONTENT_DATA.md`
- 排程与结算规则：`docs/design/GAME_DESIGN.md`
- 代码范式：`scripts/Content/Encounters/`
- 通用定义与结算：`scripts/Domain/Definitions/`、`scripts/Application/Encounters/`

## 定义检查

- 明确 EncounterKey、中文名、类型、轮次范围、基础权重和遭遇等级。
- 商店等级不等于商品卡牌等级；商品池应通过通用筛选条件表达。
- 可选项的 option key 在本遭遇中唯一；每个选项恰好属于一个展示槽。
- 随机槽明确候选权重。奖励无法创建时的行为应遵循已有自动获得与待领取规则。
- 优先复用现有 `EncounterOptionEffectDefinition`。新效果必须是可复用的结算动作，不能在服务中按 EncounterKey 分支。
- 新遭遇应生成 `scripts/Content/Encounters/<Name>EncounterDefinition.cs` 及对应 `.cs.uid`，排程、选项与效果只在 Definition 中维护；字段或规则变化时更新 `CONTENT_DATA.md`。
- 验证定义字段、排程资格、展示槽/权重和每种选项的状态变化或失败行为。

## 导师遭遇包装

- 使用 `MentorEncounterDefinition`，类型为 `Other`，只引用导师并维护遭遇配置；技能筛选留在独立导师定义中，具体流程见 [mentors.md](mentors.md)。
- 校验 `MentorKey` 对应已注册导师；遭遇等级决定本次导师及授予技能等级。仅增加独立导师时，不自动添加遭遇包装。
- 导师选项由 `MentorService` 动态生成，不配置普通 `ChoiceEncounterDefinition` 的固定展示槽；验证排程、引用和等级传递。
