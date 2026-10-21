# 星象旅馆棋盘素材

## 当前完整HTML方案v4

以实际HTML的卡牌、头像、技能、全部文字数值和操作为布局基准，采用正俯视。当前仅加载celestial-inn-complete.css；v1～v3的独立board/content样式不加载。底层为celestial-inn-orthographic-v4.png，1600×950绘制于画布x=0、y=40，矩形台面与两排卡牌平面平行；旧透视背景及陈设不接入。前景复用mount-v2与gauge/raised-v3，保持实际内容独立，不烘焙文字和数值。

底图使用内置image_gen生成。提示词要求：STRICT EXACT ORTHOGRAPHIC TOP DOWN CAMERA perpendicular to board, no perspective, trapezoid, vanishing point or foreshortening; rectangular tabletop constant width, axes parallel to canvas. Warm desaturated walnut / bronze / taupe sandstone. Actual front-facing card images, hero portraits, skills, text and numbers will be live HTML. Quiet reserved zones for two real card rows, top opponent HUD, bottom hero/resources/skills/stats/economy/reserve and right action. No baked UI, props, text, numbers, cards or portraits. 3D depth from bevel/contact shadow only, never perspective. 生成结果按实际原型内容区定位，不把提示坐标当作精确输出。

已完成脚本和资源引用静态检查及功能区域间距计算；当前浏览器工具没有原型标签，未完成完整页面视觉和输入验收。v1～v3以下记录为历史候选，不能作为当前视觉通过证据。

## 前景修订v3

用户指出v2前景仍显平面。根因为将横向素材整体压缩到24／36像素高，斜面与侧壁也随之被压扁。v3以同一完整构图为参考生成两种具有明显实体厚度的外壳，保存透明源celestial-inn-hud-v3.png，按Alpha边界裁出gauge（1747×256）及raised（1306×342）。CSS使用border-image九宫格，固定边角、斜面和下侧壁的输出厚度，资源外壳从控件向外延伸8像素但不改变命中或布局；其他控件只向外延伸3～6像素。头像支架保持比例，接触阴影统一向右下；资源填充、文字、数值和技能原画仍为独立HTML。

生成提示词：Create a transparent production sprite sheet of TWO front-facing volumetric 3D fantasy HUD housings, derived from the reference walnut bronze material and warm upper-left light. Physical carved game UI, NOT flat line art or thin outlined rectangles. Top row: long narrow 8:1 gauge, carved walnut capsule, chamfered aged brass, bright top-left bevel, visibly extruded dark lower side wall, substantial end caps, deep empty dark recessed channel for live fill. Bottom row: raised 4:1 button/plaque, broad stone face, sculpted upper bevel, visible lower side wall, cast shadow, designed for nine-slice scaling preserving corners. No text, numbers, symbols, fill colors, cards, portraits or background. Transparent outside silhouettes; strong depth and ambient occlusion, painterly rendered 3D. 参考celestial-inn-composition-v2.png，内置image_gen生成。

状态：已检查源素材、裁切、静态资源与脚本；完整浏览器组合及输入验收尚未完成，不把素材通过视为页面视觉通过。

## 场景版本v2

2026-10-21。v1被用户否定，不再接入；以下v1记录仅保留素材来源。v2全部由内置image_gen按同一完整构图参考生成，没有接入先前独立生成的前景候选。

| 文件 | 作用与拼接位置（1600×900逻辑画布） |
|---|---|
| celestial-inn-composition-v2.png | 完整构图参考，不作为页面背景直接使用 |
| celestial-inn-environment-v2.png | 环境、桌面及其边框，统一图层；1700×900居中，y=90，画布裁切 |
| celestial-inn-props-v2.png | 透明四角素材源；按象限的Alpha边界裁出prop-nw/ne/sw/se-v2.png |
| celestial-inn-prop-*-v2.png | 每件保持比例放入160×190区域；左／右18，上160或下24，独立定位 |
| celestial-inn-hud-v2.png | 同参考生成的透明四件HUD素材集，按Alpha边界裁出以下部件 |
| celestial-inn-mount-v2.png | 上下英雄支架，320×151；中心x=800，上y=79、下y=761，头像保留独立HTML |
| celestial-inn-plate-v2.png | 资源条外壳、属性／技能容器、顶部进度及商品信息底板，匹配既有控件矩形 |
| celestial-inn-button-v2.png | 经济与备战外壳100×74，对应x=216／1284，y=760 |
| celestial-inn-action-v2.png | 主操作与菜单等按钮外壳；主操作x=1392、y=462、160×56 |

前景与陈设均保持透明，文字、数值、语义图标、填充比例和命中区域不烘焙进素材。所有装饰均不接受鼠标输入。裁出部件只移除周围空白，不重绘材质。CSS中v1边框与SVG均不再引用。

### v2生成规格与提示词

统一参考提示要求：complete fantasy card autobattler board environment composition reference, 16:9 top-down orthographic, premium Hearthstone hand-painted 3D quality, original celestial inn, consistent walnut / taupe sandstone / aged bronze with warm upper-left light, empty card rows, small peripheral props, full foreground hero mounts and resource / economy / reserve / action housings, no text, numbers, icons, cards or portraits. 先定义整体构图再拆层，统一比例和材质，不允许独立风格拼凑。

环境派生提示：Using the supplied image as the sole visual reference, derive the MATCHING BASE ENVIRONMENT LAYER. Preserve exact material craftsmanship, walnut, taupe sandstone, aged bronze, lighting upper left, painted 3D style. Remove all foreground portrait mounts, HUD plates and all corner props, replace with matching wood. Keep only floor, quiet sandstone tabletop and restrained frame. Two card rows must fit fully on stone; lower band bare wood for independent HUD. No text, cards or UI housings. 生成坐标不视为精确输出，以实际HTML适配后的位置验收。

陈设派生提示：Extract/recreate only the FOUR CORNER SCENERY CLUSTERS from supplied reference as a transparent sprite layer. Match exact painted material, warm upper-left light, physical scale, walnut bronze stone textures. Lantern and ivy, armillary sphere, book, brazier; everything else transparent. No table, frame or foreground UI, no center objects, no text, no shadows invading central play area. Small peripheral clusters, no cropped silhouettes.

HUD派生提示：Create a TRANSPARENT 2x2 SPRITE SHEET of four separate reusable foreground UI housings derived from supplied reference. Match restrained walnut/bronze material, muted taupe stone inset, hand-painted 3D bevels and warm upper-left light. Separate with transparent padding, no overlap. Winged circular mount with transparent aperture; horizontal quiet dark wood plaque; almost square dark button; horizontal action button with taupe stone inset. No text, numbers, icons, portraits or table. Orthographic top-down, same material and scale.

### v2检查范围

已检查生成图、裁切与透明度，素材拼接检查图为output/board-redesign/cohesive-layers-v2.png；该图不包含真实HTML卡牌、数值和输入状态，不是浏览器截图。完整HTML组合画面与鼠标／键盘、商店／战斗／备战切换尚待验收，BUG-362保持待验证。

---

## v1弃用记录


2026-10-21。使用内置image_gen生成，当前仅用于HTML候选，未同步Godot。现行制作规则见[UI_SYSTEM](../../UI_SYSTEM.md#棋盘场景素材与分层制作)。

## 图层与拼接

| 素材 | 用途 | HTML位置 |
|---|---|---|
| celestial-inn-base-v1.png | 1672×940，不透明暖砂石桌面 | match-table底层，铺满1600×900画布 |
| celestial-inn-frame-v1.png | 1672×941，透明木铜边框 | ::before，上55、左右100、下-5逻辑像素 |
| celestial-inn-props-v1.png | 1672×941，透明四角陈设 | 四个独立230×300窗口，原图按1000×563缩放；上角y=130，下角贴底，左右贴边 |

边框及陈设均验证包含0～255真实Alpha。装饰不参与命中；卡牌、英雄、资源条和按钮保留独立HTML。素材不包含文字和业务数值。旧celestial-inn-board.svg为弃用草案，不再由原型引用。

## 生成提示词

### 桌面底层

Production game environment asset, wide landscape 16:9, top-down orthographic fantasy celestial tavern card game tabletop, Hearthstone-like premium hand-painted 3D miniature environment craftsmanship. BASE LAYER ONLY for compositing. Fill entire image with quiet warm taupe sandstone inset tabletop, softly worn matte surface, subtle hand painted irregularities and mineral grain, gentle brighter center and darker corners, desaturated walnut and warm taupe palette. Central 80 percent must be extremely quiet for placing live playing cards. Outer narrow edges reveal dark walnut planks. No frame, no objects, no cards, no UI, no symbols, no text or logos. Real carved stone substance, tactile stylized rendering, no vector look.

### 透明边框

Generate a single production game UI environment FRAME asset on truly transparent background, wide 16:9 canvas. Top-down orthographic view. Premium Hearthstone-like hand-painted 3D carved walnut and bronze tabletop frame for original celestial tavern card game. The frame outer bounds are x=10% to90% of image, y=17% to90%. Large completely transparent rectangular center opening x14% to86%, y23% to83%, clipped elegant corners. Substantial carved walnut perimeter 4 percent canvas thick, beautifully sculpted brass corner caps, bevels, worn wood grain, thick shadowed lower edge and amber highlights. Outside frame must be transparent as well as center hole. No tabletop fill, no props, no UI, no text, no cards. Rich tactile painted 3D quality, ornate but restrained, warm muted bronze brown. Full frame fully visible, straight-on top down, no perspective skew. Important transparent hole.

### 透明陈设

Create a transparent overlay production environment asset, wide landscape canvas 16:9, for a top-down fantasy celestial tavern card game board. Premium richly hand-painted 3D miniature diorama quality like Hearthstone, realistic sculpted forms and painterly materials, NOT vector. Four separate corner clusters on ONE transparent canvas; the vast entire middle x=13% to87% is completely empty transparent. Upper left cluster x0-12% y15-40%: carved stone ledge with warm metal lantern and ivy. Upper right cluster x88-100% y15-40%: sculpted brass armillary sphere and muted purple crystal on stone pedestal. Lower left x0-12% y74-95%: stacked worn leather books, parchment scroll, brass star ornament. Lower right x88-100% y74-95%: stone charcoal brazier with subtle amber embers and carved wood. Each cluster substantial tactile mass, polished art-directed painted game environment, dramatic ambient occlusion, restrained warm amber local lighting. Transparent everywhere else including top center and bottom center, no connecting frame, no table, no floor, no cards, no UI, no text, no logos. Top down orthographic; clusters do not extend into center live card play space.

## 验证范围

已检查生成图层、尺寸、Alpha及引用。完整HTML组合画面、不同页面状态、鼠标和键盘交互尚待浏览器验收；素材留白以实际HTML拼接坐标为准，不以生成提示中的期望坐标视为精确输出。
