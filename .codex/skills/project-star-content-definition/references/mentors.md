# 导师

## 必读位置

- 正式定义：`scripts/Content/Mentors/`；字段与规则见 `docs/design/CONTENT_DATA.md` 和 `docs/design/GAME_DESIGN.md` 的导师章节。
- 通用定义：`scripts/Domain/Definitions/MentorDefinition.cs`、`MentorEncounterDefinition.cs`。
- 选择与领取：`scripts/Application/Mentors/MentorService.cs`；验证位于 `scripts/Presentation/Verification/MentorChecks.cs`。

## 新增与修改

- 明确中文名、`mentor.` 前缀的 `StringName` key、独立访问默认等级和技能筛选规则；身份使用 `EntityAttributes<MentorIdentityAttributes>`，定义及 `.cs.uid` 放入 `scripts/Content/Mentors/`。
- 公开、非抽象、无参构造的 `MentorDefinition` 由注册表自动扫描。复用 `CanOfferSkill` 实现纯筛选，不修改对局、不消耗随机数，不在服务中按导师 key 分支。
- 区分固定传授归属和拜访者归属：指定“传授圣骑士技能”时匹配技能身份的 `paladin`，不替换为当前英雄归属，也不自动混入 `neutral` 技能。其他规则按用户定义实现。
- 独立访问用导师默认等级；遭遇访问用遭遇等级作为本次导师等级，授予技能等级等于本次导师等级。支持等级过滤、抽选和领取复用通用用例，不复制进内容定义。
- 从正式技能定义核对实际候选池。池为空时如实报告；不为凑足三选一更改现有技能归属、添加未请求的技能或重复补齐候选。
- 导师与遭遇分别定义。仅在请求范围包含遭遇包装时添加 `MentorEncounterDefinition`，引用已注册的 `MentorKey`，明确遭遇身份、等级、轮次与权重；不因新增导师自动创建包装。包装维护见 [encounters.md](encounters.md)。

## 查询、改 key 与删除

- 查询返回默认等级、实际筛选逻辑及所查等级的可选技能；筛选依赖拜访英雄时明确查询采用的英雄，缺少必要输入先澄清。没有运行快照时，不把候选池当成本次已经抽出的技能。
- 改导师 key 或删除导师前搜索 `MentorKey` 包装引用、独立访问调用及验证，按已确定范围处理依赖，不自动删除仍保留的遭遇。
- 修改技能归属、key 或支持等级会影响导师池；分类筛选关系不等于固定技能引用，不能仅凭完整 key 搜索判断没有影响。

## 验证

- 验证正式注册、身份、默认等级、符合规则及被排除的技能，并观察领取后的技能归属与等级；适用时覆盖遭遇等级传递。
- 正式技能池为空时使用内部技能夹具验证正向领取，保留空池行为检查；夹具不进入正式内容池。
- 内容变更执行主 Skill 的构建、差异检查和 Godot 回归；只改 Skill 文档时检查结构与引用即可。
