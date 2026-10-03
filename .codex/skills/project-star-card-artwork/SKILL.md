---
name: project-star-card-artwork
description: Generate missing Project Star card illustrations or restyle existing artwork, using card Definition sizes, approved visual references, and project resource integration. Use for card original art requests; excludes card gameplay changes and unrelated images.
---

# Project Star 卡牌原画

为当前项目生成或更新卡牌原画。先读取 AGENTS.md、docs/design/UI_SYSTEM.md 与目标 scripts/Content/Cards/ Definition，确认名称、主体、尺寸、元素、标签及现有插画路径；不从卡名猜尺寸。

## 比例与构图

| Definition 尺寸 | 卡面比例 | 生成构图 |
|---|---|---|
| Small | 1:2 | 竖向，建议1024×2048 |
| Medium | 1:1 | 方形，建议1024×1024 |
| Large | 3:2 | 横向，建议1536×1024 |

实际卡面规则以 UI_SYSTEM.md 为准。像素大小可因工具能力调整，宽高比必须符合卡牌尺寸。生成后读取图片实际宽高，不能只凭提示词或缩略图声称符合比例。比例不符时用图像生成工具重新构图，不拉伸、不用加边或代码裁切冒充符合尺寸。

原画覆盖整张卡面，名称、等级、元素、效果与价值由游戏叠加。图片本身不生成文字、卡框、UI、标志或水印。主体和脸、武器尖端等识别部位完整，边缘留适量空间，顶部和底部降低细节密度以适应信息遮挡。人物保持适合尺寸的姿态；长武器按宽高比调整方向，不能把所有主体机械放进方形画布。

## 画风与参考

用户当前认可的是细致明亮的手绘日式奇幻：细线条、丰富材质、柔和绘画阴影、暖光和自然色彩，人物为精致动漫比例。不要默认沿用早期粗描边Q版原画。

当前参考候选为项目内 army_priest-illustration.png、shield_bearer-illustration.png，以及物品图 small_mana_potion-illustration.png，均位于 art/ui/card-face/artwork/。先查看参考的实际图像；用户指定的新风格优先于这些候选。避免把参考角色、盾牌、营地等主体误搬到其他卡牌。

更新已有原画时：查看旧图和风格参考，旧图用于保持主体身份，风格参考用于线条、光照和材质；输入图片的角色必须在提示词中明确。允许按目标卡牌比例重新构图。缺图新建时根据 Definition 的主体与玩法编写图像规格，使用当前内置工具允许的参考机制，不臆造不受支持的尺寸或路径参数。

## 生成、检查和接入

1. 使用 imagegen Skill 与内置 image_gen，一张卡一次调用，多个主体不拼成图集。只有用户明确选择时才使用CLI/API替代路径。
2. 提示词注明目标卡、主体、参考角色、精确宽高比、构图、风格与禁用文字/UI要求。批量开始前确认范围；用户说全部时从当前正式 Definition 查出全部目标。
3. 每张生成成功立即记录卡牌key、来源文件、提示词和目标路径，保存进项目，避免中断后失去已完成结果。中断恢复先核对图片及 Definition，不盲目重生成。
4. 查看输出，检查主体识别、画风、解剖与装备、实际比例、文字与边框、卡面遮挡风险。需要修正时只调整具体问题，继续使用图像生成工具。
5. 正式文件存入 art/ui/card-face/artwork/。更新旧图时优先保存独立版本并切换对应 Definition 的 Illustration，方便对比；不删除旧图或共享占位图。新增图不能只留在 Codex 生成目录。
6. 仅修改目标卡牌的 Illustration 路径，不改变身份、数值或机制。引用使用 res:// 项目路径，不引用 ref/。Godot自行生成导入文件，不手工制造缓存。
7. 交付列出补齐/更新数量、实际尺寸和保存路径，报告检查结果；运行游戏验证或其他测试须遵循当前会话授权。提示词记录可放 output/，不建立额外正式内容表。

新卡若经用户明确允许临时使用其他比例，交付说明该例外，不将临时例外变成今后默认规则。
