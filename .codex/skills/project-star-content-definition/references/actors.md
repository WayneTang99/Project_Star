# 怪物与英雄

## 怪物

- 正式数据只维护在对应 `MonsterDefinition`；字段规则见 `docs/design/CONTENT_DATA.md`；代码范式位于 `scripts/Content/Monsters/`。
- 明确 MonsterKey、中文名、等级、战斗属性、卡牌实例、技能实例、棋盘起始格和战后奖励参数。
- 怪物复用玩家侧的卡牌、技能和战斗逻辑。不要创建怪物专属战斗分支。
- 所引用的卡牌/技能 key 与等级必须存在；卡牌布局不得越界或重叠。
- 怪物战由 `MonsterDefinition` 进入排程，不额外创建同名 `EncounterDefinition`。
- 新怪物生成 `scripts/Content/Monsters/<Name>MonsterDefinition.cs` 及对应 `.cs.uid`，卡组、技能与战斗属性只在 Definition 中维护；字段或规则变化时更新 `CONTENT_DATA.md`。
- 验证注册表跨引用、棋盘布局、战斗快照、排程资格和奖励池。

## 英雄

- 正式数据只维护在对应 `HeroDefinition`；字段规则见 `docs/design/CONTENT_DATA.md`；代码范式位于 `scripts/Content/Heroes/`。
- 明确 HeroKey、中文名、归属 key、初始等级、收入和全部战斗初值。
- 英雄不使用卡牌标签；金钱、经验和声望等对局资源不要误放进英雄身份或战斗初值。
- 新归属会影响卡牌、技能和商店的跨引用筛选，必须验证相关内容 key。
- 新英雄生成 `scripts/Content/Heroes/<Name>HeroDefinition.cs` 及对应 `.cs.uid`，身份与初始属性只在 Definition 中维护；字段或规则变化时更新 `CONTENT_DATA.md`。
- 新增英雄默认同时生成并接入独立原画，只有用户明确要求不生成时才跳过；“参数默认”不表示省略原画。使用 imagegen Skill 与内置 image_gen，每位英雄单独生成，不拼成图集。
- 生成前查看 `art/ui/heroes/` 中现有英雄原画，沿用其画风与方形（1:1）构图；用户指定风格优先。面部清晰且适合小头像，不生成文字、卡框、UI或水印。卡牌尺寸比例规则不适用于英雄。
- 生成后查看主体、构图、解剖与装备，并读取实际宽高。正式图片保存为 `art/ui/heroes/<name>-illustration.png`，在对应 `HeroIdentityAttributes.Illustration` 配置只读 `StringName` 的 `res://` 路径；不能只留在生成目录。由Godot生成导入文件；提示词与来源记录可放在忽略目录 `output/`。
- 验证身份、默认资源、战斗初值、定义注册和新对局创建；扩展已有 `HeroArtworkChecks`，检查原画纹理加载、身份到快照传递、选角预览和英雄面板显示。交付报告生成数量、实际尺寸、保存路径及验证结果。
