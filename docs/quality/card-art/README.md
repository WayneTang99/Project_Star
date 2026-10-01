# 全卡池插画验收

2026-10-01：当前全部17张正式卡牌各有一张独立原画，使用内置 `image_gen` 生成。只采用 Q 版比例、粗轮廓、块面上色和清楚剪影的画风框架；主体、服装、装饰与背景独立设计。

- [全部原画画廊](gallery.html)：点击任意卡牌查看完整 PNG。
- [Godot 全卡池卡面截图](card-pool-1920x1080.png)：全部17张卡牌按正式尺寸、初始等级和插画属性加载。
- [生成与修订提示词](../../design/card-art-prompts.json)：记录最终文件、原始提示词，以及审判之锤和钻石的风格修订。

插画字段为 `CardIdentityAttributes.Illustration`，只读 `StringName` 资源标识。正式 CSV、定义、工厂创建的实例、商店报价和对局快照均传递同一字段；卡面适配器直接加载路径并缓存纹理。空字段或缺失资源显示共用占位图。

验证结果：`dotnet build Project_Star.csproj` 为0警告0错误；Godot内验证115/115通过。新增检查覆盖全部正式卡牌和支持等级，核对 CSV/身份/实例/快照一致性与真实纹理尺寸，并验证缺图占位。已逐张检查原画与上述实际 GPU 卡面截图，17张均显示；全卡池截图布局问题已修复为 BUG-051。

截图复现：运行 `scripts/Presentation/Playtest/ComponentShowcase.tscn`，设置1920×1080窗口并附加用户参数 `--capture-card-pool`；截图模式使用同尺寸逻辑画布，保存后自动退出。

参考区文件哈希保持一致；运行时插画均保存于项目的 `art/ui/card-face/artwork/`。不采用的本次草稿保留于 `output/card-art-drafts/`，该输出目录不参与 Godot 导入。此前三张示例原画保留，新卡牌属性指向这套正式插画。

编辑器的技能 CSV/翻译 UID 重复警告已修复为 BUG-052：4份正式 CSV 保留原文件，清理35个无消费者的翻译产物；重新导入没有UID重复或翻译产物再生成，Godot115/115验证通过。
