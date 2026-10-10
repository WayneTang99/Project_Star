# 项目可读性与基本交互反馈

2026-10-10。用户明确HTML文字原本正确，问题来自项目。HTML恢复原字号、浮层尺寸及缩放，头像、独立属性色和灼伤归零隐藏保留；相关预览见[HTML记录](UI_READABILITY_HTML_2026_10_10.md)。

Godot修复：

- 详情正文18px、标题24px、标签13px、辅助14px，通常440px宽；长说明及成长任务仍滚动，刷新保留位置和展开状态。
- 恢复1600×900显示基准，窗口按既有16:9规则缩放。
- 按钮悬停高亮、按下变暗、移出恢复，反馈绘制在插画上方；菜单、操作、动态遭遇／技能／英雄入口共用原按钮状态。
- 卡牌鼠标及键盘焦点共享上浮和等级色发光，按下可见反馈；原有交易、取消、禁用和快照门控保持。

验证通过：构建0警告0错误、246/246回归；遭遇插画按钮、菜单、主操作按钮的真实鼠标正常／悬停／按下渲染差异及恢复；Space菜单／Escape返回、右键详情、鼠标及Enter购买、Space领奖、Tab出售确认／Escape取消／Enter只出售一次；三档详情截图、窗口／焦点／键盘及原生拖拽。

[项目详情](ui-html-parity/project-details-readable-1280x720.png) · [插画按钮悬停](ui-html-parity/project-art-button-hover-1280x720.png) · [主按钮按下](ui-html-parity/project-button-down-1280x720.png)。完整截图位于output/card-interactions/，日志位于output/ui-html-parity/native-feedback-*.log。HTML恢复后四档检查无脚本错误。

本轮以可读性和完整基本效果为目标，不做逐像素分析。没有新的完整人工对局；英雄头像随后已按用户继续执行要求同步到项目，六位英雄脸部取景及选角完整原画回归通过，BUG-203关闭；新属性色随后也已同步项目并通过两主题截图及回归，BUG-204关闭。

头像补齐：六张项目实际截图已检查，构建0警告0错误、246/246回归通过。[圣骑士项目头像](ui-html-parity/hero-portrait-paladin-1280x720.png)，其余五位截图位于output/ui-html-parity/native/hero-portrait-*-1280x720.png；日志为output/ui-html-parity/hero-portrait-{build,verify,capture}.log。

配色补齐：十种属性色同步到项目图标、详情词语／关联数值、卡面效果底板及生命／魔法条；生命再生与魔法再生按完整词语识别并使用独立色。两套主题实际截图、构建0警告0错误及246/246回归通过。[项目配色](ui-html-parity/attribute-colors-1280x720.png) · [蓝色桌面](ui-html-parity/attribute-colors-blue-1280x720.png)。日志为output/ui-html-parity/attribute-colors-{build,verify,capture,import}.log。

继续检查：技能、图鉴等通用富文本改为18px逻辑字号，普通悬停提示14px，随主画布等比缩放；卡牌浮层已有的屏幕字号保持。技能选中使用主题高亮底，未选中为深色底，保留等级框。技能／套装／奖励浮层打开时焦点进入关闭按钮，浮层内Tab循环，关闭恢复有效入口。

构建0警告0错误、246/246回归；六类按钮（选角、遭遇、菜单、主操作、设置、技能图鉴）实际悬停／按下／移出释放恢复通过。卡牌与技能图鉴1280×720及1920×1080的详情、筛选、搜索、排序、键盘返回通过；已获技能长说明滚到底及Tab／Enter关闭返回通过。[图鉴详情](ui-html-parity/catalog-details-readable-1280x720.png) · [技能说明末尾](ui-html-parity/skills-long-bottom-1920x1080.png)。日志为output/ui-html-parity/ui-basic-{build,verify,input,input-repeat,skills,catalog}.log。

BUG-220已定位修复：1920窗口重复关闭第10次复现，调用栈证明取消刷新重建商品，反馈层的光标更新触发重新悬停。没有选中项时不重绘，光标类型相同时不重复设置。1280×720、1920×1080、615×440各12次关闭／重新查看通过，商品节点保持；六类反馈、购买／领取／出售、原生拖拽取消及出售、246/246回归通过，构建0警告0错误。临时调用栈诊断已移除，保留无选中取消不重建商品及连续关闭检查。日志为output/ui-html-parity/escape-{reproduce,build,verify,fixed-input,native-drag}.log；完整人工对局仍未新增。

星辉蓝控件补齐：普通按钮、搜索框、筛选菜单、选角与技能选中底色及固定操作按钮跟随主题。两档图鉴切换／恢复保留筛选、排序、预览等级和焦点；两主题按钮真实反馈、246/246回归及构建0警告0错误通过。[技能图鉴](ui-html-parity/skill-catalog-palette-blue-1280x720.png) · [卡牌图鉴](ui-html-parity/card-catalog-palette-blue-1280x720.png)。导师旧截图夹具恢复正式1600×900逻辑基准，[小窗完整画布](ui-html-parity/mentor-canvas-1280x720.png)已检查。日志为output/ui-html-parity/palette-controls-*.log及page-feedback-mentors.log。BUG-224出售断言曾失败，随后诊断复查通过，仍保留Open；按用户要求停止扩展细节检查。
