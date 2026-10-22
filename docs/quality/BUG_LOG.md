# BUG_LOG

| 编号 | 日期 | 位置 | 描述 | 根因 | 修复方案 | 状态 |
|---|---|---|---|---|---|---|
| BUG-366 | 2026-10-22 | CardDisplayAdapter / 莫娜森林卡牌文案 | 新增相邻摧毁效果的详情格式化抛出异常；蕈灵同次发动的来源英雄再生会沿用敌方能力目标描述。 | 新通用效果未加入描述映射，状态目标描述未识别新增TargetsSourceHero。 | 补充相邻尺寸摧毁描述及来源英雄状态目标映射；专项验证蕈灵己方再生文案，并运行全卡各级文案及冰火精灵回归。 | Fixed |
| BUG-365 | 2026-10-22 | BattlePlaybackPresenter.Project / 战斗内元素投影 | 无任务卡牌的战斗有效元素改变后，回放卡面仍显示原元素。 | 元素投影只在Quests非空分支读取冻结元素。 | 全部卡牌读取战斗快照中的有效元素，空集合兼容原身份；元素腰带无任务回放与战前快照隔离专项通过。 | Fixed |
| BUG-364 | 2026-10-22 | TwoHeadedSkeletonChecks.Capture | 新增卡牌截图中关联【骷髅】显示未找到效果。 | 捕获夹具只注入等级预览查询，遗漏关联卡牌正式目录；非正式游戏入口缺陷。 | 捕获时同时注入CardCatalogQuery目录，重新检查关联骷髅说明；不改运行时UI。 | Fixed |
| BUG-363 | 2026-10-22 | 学者装备新原画导入 | 首次原生原画验收未通过mipmap检查，缩小卡面缺少完整纹理采样链。 | Godot新纹理默认未启用mipmap，首轮导入尚未补齐项目要求。 | 三张学者原画启用mipmaps/generate并保持limit=-1，重新导入；真实GPU加载均为10层，卡面和详情截图复核通过。 | Fixed |
| BUG-362 | 2026-10-21 | HTML celestial-inn-board.css / 棋盘分层方案 | 用户反馈场景多层素材互相遮挡、风格尺度不协调，前景HUD仍保留不匹配的CSS硬绘制外壳；v2整图拉伸又压扁资源框，前景仍显平面；后续误将“前景图片、文字”继续理解为外壳，遗漏图片受光与文字材质。 | 独立生成后任意缩放，未建立整体构图及功能区域留白，单层检查替代了完整页面验收；可伸缩前景没有保留斜面和侧壁厚度。 | 以同一完整构图参考重制环境、陈设和HUD外壳；按功能矩形分区拼接，移除旧硬绘制外壳；完整HTML组合及交互验收通过前保持待验证；v3补充按比例生成的立体外壳，以九宫格保留斜面及侧壁，统一接触阴影和填充受光；v4按完整游戏内容统一排版与正俯视，complete.css替换旧board/content叠加，背景不再单独使用透视；无可访问浏览器标签，静态检查不视作浏览器验收通过。 | Pending Verification |
| BUG-361 | 2026-10-21 | CardInteractionChecks.DirectAcquisition | 虫蛊袋额外全量运行中，“单一卡牌详情、直接购买与奖励领取、右键及旧节点门控正确”返回失败；其他30批通过。 | 待定位，当前日志仅有失败项名称，未提供具体断言；虫蛊袋专项与固定战斗记录通过。 | 后续针对该UI检查单独复现，核对最新悬停交互约定；本次不扩大内容任务范围。证据output/gu-insect-pouch/full.log。 | Open |
| BUG-359 | 2026-10-21 | MatchShell.DescribeCard / 图鉴套装详情 | 魔导士权杖等套装卡牌在图鉴悬停时缺少套装标识与阈值说明。 | 图鉴分支直接返回原始快照，跳过CardPreviewQuery.Describe的套装说明补全。 | 图鉴仍调用只读补全查询，但传入空对局上下文，展示正式套装阈值与0件，避免串入当前对局状态；全套装成员及权杖真实悬停回归。 | Fixed |
| BUG-360 | 2026-10-21 | CardItemView.ExitHover / 图鉴详情 | 鼠标离开详情后浮层不消失，阻挡其他卡面。 | 来源卡牌保留键盘焦点时，MouseExited跳过HideHoverCard，关闭计时始终未启动。 | 鼠标离开始终启动延迟关闭，详情内保持及滚轮继续复用既有命中判定；新增聚焦卡牌退出回归，修复前复现。 | Fixed |
| BUG-357 | 2026-10-21 | PhaseOneVerification.RunBatches | 用户截图显示3/4批通过，却只看到失败批次名称，无法辨认具体失败项。 | 批次运行后只收集布尔值，最后以批次概要覆盖RunChecks的逐项结果。 | RunChecks将失败项和异常原因返回给批次汇总，按批次保留缩进明细；批次计数使用独立变量避免受明细行数影响。HTML、原生失败与异常夹具及截图检查通过。 | Fixed |
| BUG-358 | 2026-10-21 | DisplayLayoutChecks.Settings / 原生分辨率验证 | 用户截图试玩场景集成第1批失败；原生复现19/20，headless20/20。 | 验证硬编码1920×1080预览成功，但实际屏幕可用范围1920×1032，正式界面正确拒绝超范围尺寸，旧断言却判失败。 | 从界面候选中选择适合当前屏幕的分辨率，核对预览、确认保存、取消、Escape、超时和隐藏回退，并单独验证超范围拒绝；原生与headless专项通过，带渲染分类复验63/63项、4/4批通过（output/bug-357/native-fixed.log）。 | Fixed |
| BUG-356 | 2026-10-21 | PhaseOneVerification / 验证状态标题 | 重跑期间标题沿用上次通过／失败颜色，分批汇总沿用最后一批颜色。 | 开始时未清理Modulate，汇总未按总体结果设置颜色。 | 开始检查恢复中性色，分批汇总按全部批次结果设置颜色；进度条专项验证重跑和失败结束状态。 | Fixed |
| BUG-350 | 2026-10-21 | PlaytestVerification.RelevantDetailAttributes / 详情数值夹具 | 分批group-01-part-01的零攻击详情检查失败。 | 野猪正式基础攻击已改为60，验证仍硬编码旧值20；实际详情正确保留当前0。 | 将基础攻击断言同步为60，保留当前0及辅助卡隐藏无关属性的检查；group-01-part-01重跑17/17通过。 | Fixed |
| BUG-351 | 2026-10-21 | DivineSmiteChecks.DefinitionAndMentor | 至圣斩导师等级检查随技能池增多失败。 | 对完整正式池随机抽取三个候选后，断言至圣斩必定出现；该前提不符合导师抽样规则。 | 用正式至圣斩、大主教、圣骑士及一个低级技能构造明确候选目录，验证低级排除、高级传授和领取，不改变实际随机规则；group-07-part-01重跑17/17通过。 | Fixed |
| BUG-352 | 2026-10-21 | CardFinalValueChecks.NumericFallbacks | 水井1级卡面被误报非数字。 | 减少灼伤正确显示数学负号−4，int.TryParse只接受ASCII负号；旧断言漏掉该合法数值显示。 | 验证解析前仅将数学负号归一化，仍要求完整整数；group-09-part-01重跑20/20通过。 | Fixed |
| BUG-353 | 2026-10-21 | CardDisplayAdapter.EchoCondition / EchoDescriptionChecks | 回响文案专项失败，暗影斗篷通用描述遗漏首次和每场战斗限制。 | 小天使／神圣狮鹫的断言仍要求已按通用规则移除的本场有效套话；生命阈值格式化另将真实的一次性触发简化为低于一半。 | 旧断言改核对具体加成，保留方向和目标；通用阈值描述同步当前HTML的每场战斗首次低于最大生命百分比，卡牌和能力详情共用。group-09-part-04及暗影斗篷所在part-08各20/20通过。 | Fixed |
| BUG-354 | 2026-10-21 | PlaytestVerification.TransactionScene / CardInteractionChecks.Capture | 出售确认验证寻找Content/Sell失败。 | 详情整体滚动后出售按钮已移动到Content/CardContent/Sell，旧验证路径未同步。 | 更新两处验证路径，group-10-part-02重跑20/20通过，无引擎错误；证据output/verification/20261021-100852-374/。 | Fixed |
| BUG-355 | 2026-10-21 | HeroArtworkChecks.CheckVoice / 配音验证退出 | 三项断言通过，但退出报告22个ObjectDB实例与两项MP3资源仍在使用。 | 同步验证在一帧反复Play／Stop后立即退出，音频混音线程尚未处理停止队列；verbose确认泄漏对象为20个播放实例及2个音频流。 | 配音验证改为异步，在控件释放后等待100ms让音频线程完成停止和释放，仍保留实际解码、重播、隐藏停播断言；group-10-part-04重跑3/3且无退出错误。 | Fixed |
| BUG-349 | 2026-10-21 | CardDetailsView / MatchShell / HTML悬停详情 | 鼠标离开卡牌后详情立即关闭，无法进入详情滚动成长任务。 | 悬停详情禁用鼠标命中，卡牌MouseExited直接隐藏，没有跨间隙或面板内悬停保持。 | HTML和原生允许悬停详情接收滚轮，离开卡牌延迟300ms，主详情及关联说明保持悬停，离开整组后关闭，拖拽立即关闭；浏览器交互与原生滚轮专项通过。 | Fixed |
| BUG-348 | 2026-10-21 | CardDetailsView / 成长任务详情 | 原木法杖三项任务被压入狭小的正文滚动区。 | 详情为规则正文单独限高，关联说明移到下方时再次削减正文预算，标题与页脚占据固定空间。 | HTML和原生将完整详情设为单一滚动区，内部规则、任务和属性按内容展开，关闭按钮固定；1280×720与1920×1080检查任务高度、末项可达、刷新和边界通过。 | Fixed |
| BUG-347 | 2026-10-20 | ElementSequenceResolver / 自毁前置卡牌 | 前置元素卡牌成功发动并在效果中摧毁自身时，联动窗口未开启。 | 刷新窗口时再次排除了已被自身效果摧毁的发动者，丢弃已成功发动的事件事实。 | 以成功发动及非回响事实处理刷新，窗口来源仍须有效；四项技能两级双方自毁前置与来源摧毁失效验证覆盖。 | Fixed |
| BUG-346 | 2026-10-20 | NightThiefGarbChecks.RoundBoundariesAndPool | 旧轮初验证在2级随机获得伪装面具后误报随机状态和库存数量不符。 | 检查固定要求库存只增加1张且随机状态只推进一次，忽略奖励卡牌自身的拾取随机奖励；实际伪装面具使库存增加2张并继续推进随机。 | 待将奖励副作用纳入预期；本次使用隔离候选池独立验证旧轮初战场生效、备战排除，保留原检查。 | Open |
| BUG-345 | 2026-10-20 | CardDisplayAdapter / CardEffectRow / 南瓜灯卡面 | 生命再生回响在详情有数值，卡面却没有图标与数值。 | 卡面效果类型及固定状态映射只实现魔法再生，遗漏生命再生。 | HTML加入南瓜灯示例；原生补齐生命再生类型、映射、绿色再生图标和底板，沿用冻结数值与重复项去重；四级断言及原生卡面复核。 | Fixed |
| BUG-344 | 2026-10-20 | SkillCatalogChecks / 无头全量验证 | 全量验证报告“技能图鉴交互失败：初始等级筛选”。 | 与下方BUG-332同源：夹具硬编码最高初始等级只有至圣斩，新增复生仪式后数量假设失效，实际筛选按正式定义工作。 | 2026-10-21改为从正式目录核对全部初始等级选项、每级完整有序候选和详情支持等级，保留预览不影响筛选及只读检查；修复前复现失败、修复后专项通过，证据output/bug-344/。 | Fixed |
| BUG-343 | 2026-10-20 | 宝石追加效果 / BattleEffectResolver / CardDisplayAdapter | 再生、灼伤、中毒主效果镶嵌宝石时追加状态仅为应有值的十分之一。 | 百分比直接使用原始主值，遗漏持续状态与攻击、护甲、治疗的1∶10价值换算。 | 通用追加状态定义统一换算，战斗与预览共用；补充四种状态及属性读取回归，保留加成与狂暴顺序。 | Fixed |
| BUG-342 | 2026-10-20 | MatchShell / 棋盘底图 | 通用颜色已改暖色，棋盘外围仍显示深蓝。 | 古铜砂岩PNG底图包含不透明冷色背景，覆盖主题底色。 | HTML外围渐变改暖灰；原生对棋盘绘制中的冷色像素按原亮度转为暖灰，保留古铜砂岩及子控件语义色；实际GPU截图复核。 | Fixed |
| BUG-341 | 2026-10-20 | CardDetailsView / 详情定位 | 悬浮详情及关联词条遮挡来源卡牌。 | 仅左右翻转后限制窗口边界，空间不足时把整个详情组挤回来源卡牌。 | 按右、左、下、上检查整组边界及来源避让；横向不足时关联说明可放详情下方。HTML与原生同步，1280×720和1920×1080两张长短规则卡牌、三个锚点检查通过。 | Fixed |
| BUG-340 | 2026-10-20 | CardPreviewView / 预览列对齐 | 不同卡牌的卡面高度位置随规则长度变化。 | 卡面列默认填充整行高度，未独立垂直居中。 | 卡面、规则及关联说明各列独立垂直居中；两种分辨率下大教堂、苍穹塔和死亡之手德比利斯几何检查通过。 | Fixed |
| BUG-339 | 2026-10-20 | CardPreviewView / CardDetailsContent | 放大预览规则内又嵌套规则及属性滚动。 | 复用悬浮详情的限高滚动组件，外部预览同时已有滚动容器。 | 嵌入预览时内部规则、套装和属性按内容展开，仅保留外层规则滚动；HTML同步，长套装与属性展开原生检查通过。 | Fixed |
| BUG-338 | 2026-10-20 | ComponentShowcase.CaptureLobby / 选角验证 | 实际选角及单英雄显示正确，但夹具误报选择失败。 | Godot将Node.Name中的点替换为下划线，夹具把经过规范化的节点名当作正式hero key比较。 | 选角按钮使用无点的展示节点名，夹具显式添加hero.前缀比较正式key；真实鼠标、六英雄逐一选用、只显示一个英雄及旅程提交次数验证通过。 | Fixed |
| BUG-337 | 2026-10-20 | CardDisplayAdapter / CardEffectRow | 巫师装备的魔法再生实际生效，但卡面只显示回魔或消耗，遗漏再生数值。 | 通用卡面效果映射未覆盖ManaRegen，缺少对应效果种类。 | 先在HTML恢复机制示例中加入两张巫师装备，再同步原生ManaRegen效果映射，复用紫色魔法底板和循环再生图标；四级显示断言和原生大小卡面截图验证。 | Fixed |
| BUG-336 | 2026-10-19 | HTML / 世界图鉴详情 | 选择条目打开详情时列表变窄重排，窄屏原列表消失。 | has-detail切换网格列数及列表display。 | 详情改为独立浮层，列表保持原有尺寸与显示方式；三尺寸打开及关闭详情前后条目矩形一致。仅HTML修复。 | Fixed |
| BUG-335 | 2026-10-19 | HTML / 大厅英雄选择 | 选中英雄时姓名牌宽度改变，底部身份内容可能挤压操作区。 | 选中标记插入文字流，底部身份和操作共享弹性宽度。 | 标记改为绝对定位并预留空间，身份区固定列宽，操作区独立；三尺寸逐一选择六英雄，姓名牌及底部矩形与字号均保持一致。仅HTML修复。 | Fixed |
| BUG-334 | 2026-10-19 | LavaStoneChecks / 原生卡面验证 | 新增熔岩石验证初稿编译时找不到CardItemView。 | 缺少Presentation.Playtest命名空间导入。 | 补齐using并重新构建及执行专项验证。 | Fixed |
| BUG-333 | 2026-10-19 | PhaseOneVerification / 无头全量UI检查 | 内容增长后全量回归积压回调、内存增长并在图鉴重建时原生崩溃。 | 同步在一帧运行全部检查，图鉴筛选也在同帧反复销毁／重建大量卡面，QueueFree和延迟回调没有处理机会；归属断言还对每个条目重复扫描全定义。 | 2026-10-21改为每批最多20项、检查间分帧、图鉴每次筛选重建后分帧；正式目录每次检查只读取一次。新增独立进程批次脚本、180秒默认超时、逐批日志及失败汇总。修复前专项复现0xC0000005；分批469项全部执行结束，无崩溃／超时，图鉴所在批20/20通过。其余断言及资源错误另登记BUG-350—355。证据output/bug-333/与output/verification/20261021-100335-297/。 | Fixed |
| BUG-332 | 2026-10-19 | BattlePlaybackPresenter.Project / 尺寸变化转变 | 中型卡原位分裂后，回放投影新增第二张小型卡会移除第一张的棋盘占位。 | 转变只替换展示身份，保留原中型两格占位；随后新增卡按重叠清理时误删第一张。 | 消费CardTransformedEvent时先按冻结转变身份更新原占位宽度，再处理新增卡；验证原位两张小型、相邻卡保留、战前及对局快照隔离。 | Fixed |
| BUG-331 | 2026-10-19 | QuicksandExpanseChecks.OutcomesAndLevels | 新增弓箭塔后流沙奖励验证失败，奖励和确定性结算正常。 | 测试假定随机奖励后战场恰好1张卡，抽到训练营的拾取奖励会额外放置卡牌。 | 改为检查本次奖励卡实际位于战场，继续验证等级、归属和确定性，允许正常拾取连锁。 | Fixed |
| BUG-323 | 2026-10-16 | CardDisplayAdapter / 卡牌详情与任务奖励 | 发动描述统一添加“目标：”前缀，导致己方英雄重复、自身疾速缺少主语，描述中残留冒号。 | 能力层统一目标与自带目标的效果文案叠加，状态描述未按实际目标生成。 | 改为按效果补必要目标，去掉统一目标前缀与冒号，任务奖励同步；全卡全部支持等级与代表原生预览检查。 | Fixed |
| BUG-324 | 2026-10-16 | CardDisplayAdapter / 教团远征军与战斗状态 | 远征军光环显示内部英文属性key，战斗详情使用英文秒单位。 | 敌方标签计数成长分支直接插值AttributeKey，状态行硬编码s。 | 统一使用中文属性映射，时长单位改为秒；全卡逐级英文残留审查。 | Fixed |
| BUG-325 | 2026-10-16 | CardKeywordText / 召唤、转变与指定卡牌奖励 | 引用卡名与普通正文混在一起，卡名中的属性词还会被拆开高亮。 | 只识别效果关键词和身份标签，未识别具体卡牌引用。 | 具体引用生成【卡名】，HTML与原生优先整段金色加粗；覆盖固定及随机召唤、转变和指定卡牌获得。 | Fixed |
| BUG-322 | 2026-10-16 | PhaseOneVerification / 重绘截图入口 | 新增圣骑士重绘截图参数启动后未截图、未退出。 | 参数处理误放入RunChecks尾部，普通截图启动不会调用该方法。 | 将参数处理移至现有截图参数所在的场景初始化入口，重新构建并执行三卡实际纹理与预览截图。 | Fixed |
| BUG-321 | 2026-10-16 | CardPreviewQuery.Side / 装备店铺等级预览 | 从2级切换3、4级时，装备店铺攻防光环显示为0；其他价值光环同样受影响。 | 等级预览只传战斗属性，遗漏快照中的当前价值，FrozenValue默认为0并覆盖卡面计算。 | 预览输入补传卡牌当前价值；增加装备店铺及黑市典当铺全部支持等级双向切换、永久成长保留与卡面贡献回归。 | Fixed |
| BUG-320 | 2026-10-16 | HeroSelectionView / SkillCatalogView | 选角英雄顺序、技能列表与归属筛选不符合卡牌图鉴的统一归属顺序。 | 选角使用输入顺序，技能图鉴沿用查询顺序，卡牌图鉴独立维护排序。 | 三个界面复用表现层归属顺序；技能同归属按初始等级升序，补充真实控件排序与交互回归。 | Fixed |
| BUG-319 | 2026-10-16 | 新卡原画导入 / 战舰舵手、大天使号 | 新增原画导入未启用mipmap，缩小显示不符合现行采样约定。 | 新PNG默认导入配置仍为mipmaps/generate=false。 | 两张本次会话新增原画启用mipmap，重新导入并复查原生卡面。 | Fixed |
| BUG-318 | 2026-10-16 | PlaytestVerification.EventModifierRefresh | 新增战舰舵手后全量397/398，校场加成检查找不到攻击卡。 | 验证依赖完整正式卡池的随机三件商品必有攻击卡，新增辅助卡改变抽样结果。 | 通过应用用例获得明确的臂铠作为校场前置条件，验证继续检查同次刷新后的攻击值与卡面，不依赖随机商品构成。 | Fixed |
| BUG-317 | 2026-10-16 | MinimalPlaytest / EncounterScheduler | 早期商店全为4级，商品上限未随当前轮次开放。 | 试玩入口注入固定4级覆盖，普通排程也允许低轮商店直接升至4级。 | 移除试玩覆盖，商店访问等级按1／3／5／8轮分别开放至1／2／3／4级，5级特殊商店保留第10轮解锁；验证轮次边界、首批／付费刷新、费用与随机复现。 | Fixed |
| BUG-316 | 2026-10-16 | CardDisplayAdapter.Rules / AbilityDetails / 征服者堡垒 | 多种回响详情遗漏相邻方向、触发对象及具体状态，条件拆在句尾；神圣狮鹫状态获得回响及征服者堡垒词条错误显示光环。 | 使用通用Activation名称拼接字段，未逐种表达触发条件；底层事件反应与展示分类混淆。 | 统一从触发字段生成完整条件，明确事件目标与本场成长；事件式相邻状态反应显示回响，修正堡垒词条，增加全卡逐级审查及代表截图。 | Fixed |
| BUG-315 | 2026-10-16 | CardPreviewView.RenderPreview | 右键大预览详情缺失实际标签，冷却仅混在卡面下方小字中。 | 为去除重复卡名隐藏了整个HeaderMargin，连同标签与冷却一起隐藏。 | 保留详情标题区，仅隐藏重复卡名；标签持续显示，冷却改为68px三行圆徽，HTML同步。 | Fixed |
| BUG-314 | 2026-10-16 | BattleEffectResolver.ChargeCard / CombatSimulator | 回响充能至零后的主动发动仍能继续触发回响，两张短CD卡可同Tick反复互充。 | 立即充能发动未携带回响链截断信息。 | 为充能产生的主动队列记录独立的回响截断标记并传递至多重，保留正常魔法支付、冷却重置和主动效果。补充辰光之门互充及后续正常发动回归。 | Fixed |
| BUG-313 | 2026-10-15 | 原生拖拽路由与备战抽屉 | 新布局中备战与阶段共用上方区域，旧出售遮罩和直接向隐藏备战坐标拖拽会误出售。 | 旧目标区按左侧英雄栏／上方遮罩固定分派，没有新布局的边缘缓冲与抽屉可见性。 | 路由优先真实棋盘与宝石，边缘24逻辑像素取消；悬停展开、离开收起，提示随指针；原生跨区、取消、出售与镶嵌验证通过。 | Fixed |
| BUG-312 | 2026-10-15 | 原生截图／窗口验证夹具 | 截图以右键预览代替固定详情；长说明夹具只更改旧描述字段而无可展示能力，导致无法覆盖正文滚动；新HUD可聚焦控件被旧Button断言拒绝。 | 夹具没有跟随现有右键／悬停分工、能力生成文案与HUD焦点类型更新。 | 使用明确的固定详情入口、足量通用能力只读样例和Control焦点断言；多档分辨率、缩回、正文滚动及键盘验证通过。 | Fixed |
| BUG-311 | 2026-10-15 | PlayerHeroPanel / 头像圆形遮罩 | 使用AtlasTexture裁切的英雄头像仍显示为方形，覆盖圆环。 | 原圆形材质按原画UV计算半径，AtlasTexture的UV不是控件局部0～1。 | 用PortraitClip按90×90局部坐标计算圆形，保留原画取景；实际原生截图确认圆形与多层头像框。 | Fixed |
| BUG-310 | 2026-10-15 | ResourceBar / 填充与金属边框 | 资源条填充盖住边框，生命仍沿用旧紫红资源，护甲再生留在条外文字行，层次不符HTML。 | 原ProgressBar填充铺满矩形，未分离边框与内凹区域，旧资源及独立文字布局未同步。 | 填充内缩4像素、保留双层金属框；按当前生命／魔法色生成渐变，护甲与+再生移至生命条两端，HUD符号直接提取自HTML。 | Fixed |
| BUG-309 | 2026-10-15 | MatchShell / HTML同步 | 原生仍采用左侧英雄与三排棋盘，缺少当前HTML的砂石桌面、备战抽屉及分层材质。 | 原生布局停留在旧两列设计。 | 按当前HTML同步上下区域、底部居中HUD与右侧主操作；皮肤纹理／材质独立于布局，只保留默认砂石主题；规则、窗口、拖放及菜单专项验收。 | Fixed |
| BUG-308 | 2026-10-15 | game-16x9.html / 底部两侧面板 | 金币面板圆角过大，与右侧备战入口尺寸及位置不对称。 | 两侧使用独立宽高、偏移与圆角。 | 两侧统一100×74、6px圆角与相同材质，相对1200宽英雄区各内缩16px、顶部同为10px；备战文字与数量分行；隐藏己方战场标题及占格文字。脚本及几何静态检查通过，仅HTML。 | Fixed |
| BUG-307 | 2026-10-15 | game-16x9.html / 战斗页面生成 | 切换战斗后敌方战场未生成。 | 删除战斗说明段落后，旧增强脚本仍对不存在的`.battle-text p`赋值，抛出异常，中断后续敌方摘要迁移及战场生成。 | 删除过期赋值，检查完整战斗渲染链在无说明段落时能生成敌方卡牌区；仅修改HTML，未同步原生。 | Fixed |
| BUG-306 | 2026-10-15 | game-16x9.html / 战斗备战入口 | 展开备战区会隐藏上方阶段区，战斗模式下也可能覆盖敌方战场。 | 备战展开未按战斗模式限制。 | 战斗模式禁用备战入口并在展开函数内拦截，切换模式收起备战；UI规范固定保留双方战场。脚本与模式门控检查通过，仅HTML。 | Fixed |
| BUG-305 | 2026-10-15 | game-16x9.html / 卡牌拖放 | 拖动只有出售判断，棋盘内缺少真实摆放落点，边缘空白容易误判为出售；接收位置也没有独立视觉状态。 | 旧拖动处理仅创建跟随卡面并命中出售，未区分放置计划、空间不足、镶嵌与边缘缓冲。 | 增加连续占格插入预览与战场／备战移动，释放前不改数组；青色落点、红色禁放与琥珀出售反馈分离，棋盘边缘24逻辑像素作为取消缓冲，移入备战入口350毫秒后展开；放置／镶嵌优先于出售，移动保留对应实例宝石。专项逻辑及脚本检查通过，仅HTML，实际浏览器拖放待验收。 | Fixed |
| BUG-304 | 2026-10-15 | game-16x9.html / 宝石商店 | 两块宝石选项像高立板，内容超出固定阶段框并触发纵向滚动条。 | 宝石选项复用stage-body嵌套布局及其最小高度，图标、技术说明与间距累计超过230px。 | 移除嵌套stage-body与常态长说明，以156px高的图形宝石、名称和费用排列；提示18px、行间距12px与上下边距合计210px，效果详情移至悬停说明。脚本及结构／高度静态检查通过，仅HTML，浏览器截图待验收。 | Fixed |
| BUG-303 | 2026-10-15 | game-16x9.html / 备战区展开 | 备战抽屉横在中间，遮挡己方战场卡牌。 | 抽屉按浮层定位到两个战场之间，未为展开状态安排独立内容位置。 | 备战区复用上方250～480的固定内容区域，展开时临时隐藏遭遇内容，关闭后恢复；己方战场保持500～730不动。展开／关闭状态切换与几何检查通过，仅HTML，浏览器视觉待验收。 | Fixed |
| BUG-302 | 2026-10-15 | game-16x9.html / 打开备战按钮 | 右下角备战按钮可见但无法点击。 | 覆盖整个局内区域的透明layout-main在英雄区域之后绘制，截获底部按钮的鼠标事件。 | 透明布局容器不再参与点击命中，仅阶段和棋盘子区域接收输入；英雄控件层提升至内容容器之上，备战抽屉仍在更高层。展开、再次点击收起、关闭按钮、Escape及脚本检查通过；未完成浏览器命中截图复核，仅改HTML。 | Fixed |
| BUG-301 | 2026-10-15 | game-16x9.html / 右侧主操作与底部HUD | 分叉桌沿压住主操作按钮，成长数值、技能与经济区贴近桌沿且部分数字对比不足。 | 主操作位于英雄组件的独立堆叠上下文内，子层级不能越过外侧分叉；底部按外桌沿而非台面内沿定位，浅桌面与深色数字缺少独立底色。 | 主操作移到画布直接子层并简化为独立尖角“继续”按钮，移除重复离开入口；底部常规控件退至y=836以内，保留至少16px内沿间距，头像位置保持；成长与技能使用等宽等高深色底色和浅色数字；商品、奖励只保留卡面，额外信息进入悬停详情。脚本、层级、几何和对比度静态检查通过；尚未完成浏览器截图验收，仅修改HTML。 | Fixed |
| BUG-300 | 2026-10-15 | game-16x9.html / 敌我战场卡面 | 布局用固定卡高与横向网格拉伸组合，导致中型卡牌不再为1:1。 | 卡牌横向随网格变化，高度独立固定，未按尺寸保持宽高比。 | 敌我战场均使用204像素卡高，小／中／大卡宽102／204／306，网格仅控制位置，内容区随卡面预留230像素高度；上下区域继续围绕y=490对应。比例、脚本及几何静态检查通过，浏览器视觉待验收。 | Fixed |
| BUG-299 | 2026-10-15 | game-16x9.html / 底部英雄HUD | 英雄状态、经济与备战入口各自绝对定位，和桌面底沿叠盖，资源条增高后下方信息也缺少承载空间。 | 新桌面与旧独立底部控件使用不同定位范围，未约束常规控件和台面内沿的间距。 | 按同一英雄底座坐标组织两翼资源及信息组件，经济与备战入口退进台面；护甲及生命再生移到血条两端，使用专属色数字；进一步的层级及对比度修复见BUG-301，仅HTML完成，实际视觉待验收。 | Fixed |
| BUG-298 | 2026-10-15 | game-16x9.html / 局内桌面布局 | 上下区域虽等分，但桌面、头像和战场分别定位，整体未围绕桌面中心对称。 | 桌面止于己方头像上方，敌我战场宽度、间距不同，桌面分界线也偏离局内中线。 | 统一以y=490为镜像轴，桌面110～870、头像110～200与780～870、内容区254～470与510～726；敌我战场统一1200宽、216高，己方标题移至战场下方；几何及脚本静态检查通过，浏览器视觉待验收。 | Fixed |
| BUG-297 | 2026-10-15 | 正义天使俯冲斩击原画 | 手中剑柄、护手与剑刃透视不一致，前景宽光带使剑身看似断开。 | 生成图将夸张挥斩光带与实体剑混合，武器未保持统一中轴。 | 使用图像编辑重建同轴剑柄、护手和剑身，调整双手握位并移除宽光带；接入独立修正版，目视检查及1254×1254比例检查通过。 | Fixed |
| BUG-295 | 2026-10-13 | game-16x9.html 元素徽标 | 部分元素缺少独立渐变，土／雷／冰缺少符文和中文名称映射，无法完整呈现十种元素。 | 原型仅维护部分元素样例，样式和映射未覆盖全部正式元素。 | 补齐十种名称、符文与渐变，风与原生共用卷风矢量资源；HTML脚本语法及原生全元素截图检查通过。 | Fixed |

本文档记录项目每次 BUG 的发现与修复记录。发现即登记，修复后补全根因与修复方案。

历史位置与行号按发现时版本记录；目录整理后的现行模块见架构文档，原文件和验证入口可在 Git 历史定位。

## 登记约定

- 编号：`BUG-001` 格式，零填充，单调递增，不复用。
- 状态：仅限 `Open` / `Fixing` / `Fixed` / `Won't Fix` / `Duplicate`。
- 位置：`文件路径:行号` 或 `模块/类名`，保证可定位。
- 根因与修复方案：发现时可留空；状态改为 `Fixed` 时必须填写。
- 发现即登记，不等修复。

## 记录表

| 编号 | 发现日期 | 位置 | 问题描述 | 根因 | 修复方案 | 状态 |
|---|---|---|---|---|---|---|
| BUG-001 | 2026-09-19 | `Project_Star.Tests/Application/Common/ResultTests.cs:9` | 普通 xUnit 测试进程未初始化 Godot 运行时，构造 `StringName` 时触发 `AccessViolationException` 并中止整个测试运行。 | 错误采用了独立 xUnit 测试宿主；该宿主不具备 Godot 原生运行时上下文。 | 移除 xUnit 测试项目；后续验证统一通过 Godot 内手动测试入口执行。 | Fixed |
| BUG-002 | 2026-09-19 | `project.godot` / 第 1 步交付 | 清理 xUnit 后未提供 Godot 主场景，用户无法执行约定的手动验证。 | 测试方式调整时只移除了错误入口，没有同步补齐替代入口。 | 新增 `Main.tscn` 和第 1 步验证界面，并将其配置为主场景。 | Fixed |
| BUG-003 | 2026-09-19 | `Playtest.tscn` / 第 8 步交付 | 遭遇排程实现后只加入规则验证，试玩主界面仍停留在最小战斗流程。 | 实现时遗漏了将新应用用例接入试玩入口。 | 在试玩场景加入遭遇生成、三选一、第 4 回合怪物战、第 8 回合 PvP 与回合推进。 | Fixed |
| BUG-004 | 2026-09-19 | `scripts/Presentation/MinimalPlaytest.cs` | 试玩仍要求玩家按开发测试步骤手动创建、购买、放置、战斗和生成遭遇，不符合正常对局流程。 | 试玩入口按功能验证顺序组织，而非按游戏状态流组织。 | 重写试玩控制器：选英雄后自动进入遭遇，并按商店、事件、战斗准备、结算和下一回合切换界面。 | Fixed |
| BUG-005 | 2026-09-19 | `scripts/Presentation/MinimalPlaytest.cs` / 商店 | 同一商店报价购买后仍可重复购买。 | 试玩商店未保存本次报价的售罄状态。 | 购买成功后标记报价售罄并隐藏购买按钮。 | Fixed |
| BUG-006 | 2026-09-19 | `Playtest.tscn` / 战斗准备 | 试玩没有提供可操作的双棋盘，只能自动把下一张卡放入战场。 | 第 5 步棋盘服务未接入正常对局 UI。 | 增加战场 10 格和备战区 10 格的点击操作。 | Fixed |
| BUG-007 | 2026-09-19 | `scripts/Application/Encounters/EncounterScheduler.cs` | 普通回合可能抽到怪物或 PvP 遭遇。 | 普通候选池未按遭遇类型排除特殊遭遇。 | 普通回合只从 Shop 和 Other 中抽取；怪物/PvP 仅走第 4/8 回合分支。 | Fixed |
| BUG-008 | 2026-09-19 | `scripts/Presentation/MinimalPlaytest.cs` / 双棋盘 | 双棋盘只在战斗准备界面显示，其他对局阶段无法调整。 | 表现层错误地把棋盘可见性绑定到战斗准备状态。 | 棋盘改为对局内常驻区域；遭遇、商店、事件、准备和结算阶段均可操作。 | Fixed |
| BUG-009 | 2026-09-19 | `scripts/Presentation/MinimalPlaytest.cs` / 商店购买 | 购买的卡牌进入了不存在于玩法设定中的库存，必须再次手动放置。 | 试玩界面错误地把底层卡牌归属集合实现成了第三个放置区域。 | 移除库存界面与手动放置入口；购买前检查双棋盘空间，成功后优先自动放入战场区，其次放入备战区。 | Fixed |
| BUG-010 | 2026-09-22 | `scripts/Presentation/PhaseOneVerification.cs` / 内容拆分 | 删除旧测试内容定义后，验证入口、试玩入口和本地测试对手仍硬编码引用旧类型与 key，导致构建失败或运行时查找失败。 | 验证和试玩错误地依赖了占位内容，而非各自使用验证数据或通用注册表选择。 | 验证入口改用内部专用定义；试玩和本地对手按稳定 key 顺序选择注册表中的正式内容，并在内容为空时提供明确状态。 | Fixed |
| BUG-011 | 2026-09-22 | `HeroDefinition` / `HeroInstance` | 英雄模型错误持有卡牌分类用的 `TagSet`，导致玩家角色与卡牌标签概念混淆。 | 早期通用定义模型将英雄和卡牌都接入了同一标签扩展点，未落实标签仅用于卡牌分类的规则。 | 从英雄定义、英雄实例和实体工厂移除标签；保留 `Human` / `Mechanical` 作为卡牌标签，并同步设计与架构文档。 | Fixed |
| BUG-012 | 2026-09-22 | `scripts/Domain/Combat/BattleRuntime.cs` / 零能力卡牌 | 正式卡牌没有配置能力时，战斗层会自动补充旧版普通攻击，导致“没有功能”的卡牌仍能造成伤害。 | 早期战斗测试使用空能力列表触发兼容攻击，但该兼容路径未与正式卡牌快照区分。 | `CardBattleSetup` 增加显式兼容标记；正式卡牌快照关闭旧版攻击，仅底层旧测试输入默认保留兼容行为。 | Fixed |
| BUG-013 | 2026-09-22 | `scripts/Content/Cards/JewelryBagCardDefinition.cs` | 珠宝袋被错误定义为只支持2级，导致实例无法升级，也永远无法在4级出售时随机获得4级钻石。 | 将“2级卡牌”误解为“仅存在2级”，没有区分初始等级与支持等级。 | 保持初始等级2，补充3级和4级配置；出售奖励继续按实例当前等级筛选材料，并增加4级钻石候选验证。 | Fixed |
| BUG-014 | 2026-09-22 | `scripts/Presentation/PhaseOneVerification.cs` / `scripts/Application/Combat/BattleSetupFactory.cs` | 内容拆分后第1～9步验证中，珠宝袋奖励与战斗快照相关验证失败。 | 验证专用战斗卡在替换旧内容时遗漏攻击属性和能力；无能力正式卡仍被旧冷却校验拒绝；棋盘满载用例只占满战场而遗漏备战区。 | 补全验证卡的通用攻击能力；允许关闭旧版攻击的零能力卡使用零冷却；满载用例同时占满战场与备战区。 | Fixed |
| BUG-015 | 2026-09-22 | `scripts/Presentation/MinimalPlaytest.cs` / 怪物战 | 野猪的零攻击卡组看起来仍会攻击玩家，且战斗准备界面看不到怪物卡牌。 | 日蚀伤害日志未标注伤害来源；试玩界面只渲染玩家棋盘，未渲染已创建的敌方战场。 | 为伤害事件记录卡牌、状态和日蚀来源并在日志中显示；增加只读敌方战场区，在战斗准备与结算阶段展示对手卡牌。 | Fixed |
| BUG-016 | 2026-09-22 | `scripts/Content/Cards/LightCavalryCardDefinition.cs` | 轻骑兵发动时把攻击伤害施加给己方英雄；军靴提供疾速后会缩短错误能力的等待时间。 | 轻骑兵把伤害与己方卡牌强化组合在同一个目标为 `AlliedHero` 的能力中，伤害效果沿用能力目标；验证只检查伤害数值，未检查目标阵营。 | 将轻骑兵主动能力目标改为 `EnemyHero`；己方人类卡牌强化继续按能力来源阵营结算，并补充轻骑兵伤害目标验证。 | Fixed |
| BUG-017 | 2026-09-23 | `scripts/Presentation/PhaseOneVerification.cs:1266` / `:1796` | 第 1～9 步验证中，臂铠和教团远征军两项错误地显示失败。 | 元素属性断言与正式卡牌定义及 `docs/design/CARD_DATA.md` 相反：臂铠被要求为 Light，教团远征军被要求为 General，导致战斗效果检查前直接返回失败。 | 将臂铠断言改为 General、教团远征军断言改为 Light。 | Fixed |
| BUG-018 | 2026-09-23 | `docs/engineering/ARCHITECTURE.md` / `docs/planning/IMPLEMENTATION_PLAN.md` | 文档称被动光环进入 `PendingAbility` 队列，并将已由现有模型承担的职责列为独立类型，导致第 7 步状态被误判。 | 实现采用主动/回响入队、被动光环按当前状态求值；文档没有随实现边界更新。 | 同步架构与计划描述，明确现有来源检查、魔法检查和回响截断的实现位置。 | Fixed |
| BUG-019 | 2026-09-23 | `scripts/Presentation/MinimalPlaytest.cs` / 选角 | 将试玩创建参数设为 999 后，首轮收入立即发放，玩家实际起始显示 1004 金，不符合测试目标。 | `CreateMatchService.Create` 会在创建对局时结算第 1 轮收入，试玩把目标余额误当成结算前余额。 | 试玩创建对局时从目标余额 999 中扣除英雄初始收入作为创建参数，首轮收入结算后显示 999。 | Fixed |
| BUG-020 | 2026-09-24 | `scripts/Presentation/PhaseOneVerification.cs` / `CheckMonsterRewardRetention` | Godot 规则验证的“棋盘已满时怪物卡牌奖励保持待领取”用例抛出未知阵营异常。 | 测试使用帕拉帝恩臂铠占位，但专用定义注册表未包含其阵营。 | 改用已有的无阵营兽皮占位，并复用同一兽皮定义注册到测试用定义表。 | Fixed |
| BUG-021 | 2026-09-24 | `scripts/Presentation/PhaseOneVerification.cs` / `CheckCardSetBonuses` | 套装战斗快照验证因未知套装异常失败。 | 测试创建 `BattleSetupFactory` 时漏传已注册的套装定义。 | 将同一注册表的套装定义传给战斗快照工厂；无界面规则验证 90 项全部通过。 | Fixed |
| BUG-022 | 2026-09-24 | `scripts/Presentation/PhaseOneVerification.cs` / `CheckChargeSkill` | 冲撞护甲验证错误地显示失败。 | 用例中的触发卡牌先造成 1 点伤害，已消耗敌方 1 点护甲，却仍断言冲撞吸收完整 5 点护甲。 | 将触发卡牌伤害设为 0，仅保留发动事件；无界面规则验证 93 项全部通过。 | Fixed |
| BUG-023 | 2026-09-24 | `scripts/Content/Skills/ChargeSkillDefinition.cs` / 冲撞伤害 | 冲撞被定义为按技能等级预先计算的固定伤害，不符合“自身等级”指英雄等级的规则。 | 将描述中的自身等级误解为技能等级，且战斗快照没有提供英雄等级。 | 战斗快照加入英雄等级，改用按来源方英雄等级乘技能等级系数的通用伤害效果，并以不同英雄等级验证。 | Fixed |
| BUG-024 | 2026-09-24 | `scripts/Content/Cards/HolySlashingBladeCardDefinition.cs` / 黎明之剑 | 黎明之剑的主动效果会随机摧毁敌方中型恶魔或亡灵卡牌。 | 卡牌能力配置与“仅小型”的目标规则不一致，验证用例也将中型当成可选目标。 | 将摧毁尺寸限制为小型，同步正式卡牌描述，并调整战斗验证确保中型卡牌存活。 | Fixed |
| BUG-025 | 2026-09-24 | `scripts/Infrastructure/Definitions/DefinitionRegistry.cs` / `ValidateEncounter` | 启动试玩扫描遭遇定义时，酒馆交易因无效效果异常中止。 | 校验器没有识别新增的跨英雄购买、下场战斗最大生命加成和战场卡牌属性修改效果，未知效果默认拒绝。 | 增加这三种通用遭遇效果的参数校验，并在规则验证中检查酒馆与校场定义可以通过注册。 | Fixed |
| BUG-026 | 2026-09-24 | `scripts/Domain/Combat/CombatSimulator.cs`、`scripts/Application/Encounters/ResolveEncounterOptionService.cs` / 荆棘甲攻击强化 | 校场漏掉荆棘甲；铁匠铺虽然写入其攻击属性，战斗伤害却不受强化影响。 | 将“攻击能力”错误地等同于基础攻击值大于0；荆棘甲伤害只读取己方英雄护甲，战斗状态也未登记其攻击属性为可强化属性。 | 荆棘甲伤害增加来源攻击属性；该属性登记为能力支持属性；校场识别伤害效果引用该属性的卡牌。铁匠铺和校场均验证荆棘甲攻击强化。 | Fixed |
| BUG-027 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / 卡面组件类型 | 接入 `CardFace` 后 C# 构建失败，编译器将 `CardFace` 解析为命名空间而非类型。 | `CardFace` 命名空间与其中的同名类型冲突，裸类型名引用产生歧义。 | 为控件类型添加 `CardFaceControl` 别名；`dotnet build Project_Star.csproj --no-restore` 成功，0 警告、0 错误。 | Fixed |
| BUG-028 | 2026-09-30 | `scripts/Presentation/Playtest/BoardZoneView.cs` / `CardItemView.cs` / `CardFace.cs` | 固定17%卡面导致文字约4px，中大型卡牌只占起始按钮，缩放不重排。 | 逐格按钮承载整张卡牌，整体缩放同时压缩字体，布局与数据更新耦合。 | 独立实体视图覆盖完整占格，棋盘统一格标尺；卡面按实际尺寸布置子模块并保留13px文字下限，装饰独立缩放；Resize仅重排。108项回归及原生拖拽输入通过，1280×720/1920×1080紧凑棋盘实际图已核对；其他窗口视觉仍待P6。 | Fixed |
| BUG-029 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / `CreateCardFaceViewModel` | 已有臂铠、野猪、审判之锤原画无法匹配。 | 映射使用 `armguard` 等字符串，而正式身份 key 为 `card.armguard` 等完整值。 | CardDisplayAdapter 改为完整 StringName key 映射、缓存纹理并提供统一缺图占位；Godot 4.7.2 headless 检查三张原画路径及占位复用通过。 | Fixed |
| BUG-030 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / `CreateCardFaceViewModel` | 卡面漏显示修女治疗、审判之锤百分比伤害，并忽略实例的对局属性加成。 | 仅枚举等级定义的四个基础属性，没有读取能力效果语义和实例当前只读数值。 | MatchSnapshot 复制实例基础/当前属性及能力，CardDisplayAdapter 共用卡面与详情格式化，治疗和百分比保留语义；Godot 验证治疗10、最大生命20%及校场加成通过。 | Fixed |
| BUG-031 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / `ResolveEventOption` | 普通事件获得卡牌或修改卡牌属性后，当前棋盘不能立即反映变化。 | 成功分支仅更新文字和资源栏，遗漏棋盘刷新。 | 所有操作由 MatchPresenter 执行后统一 RefreshView，MinimalPlaytest.Render 同步刷新 HUD、棋盘和详情；真实 Playtest 场景按钮驱动的垃圾场获得与校场强化检查通过。 | Fixed |
| BUG-032 | 2026-09-30 | `scripts/Presentation/MinimalPlaytest.cs` / `ChooseEncounter`、`ShowPreparation`、`HideAllActions` | 进入怪物战或 PvP 准备时，敌方棋盘数据已生成，面板仍可能保持隐藏。 | `HideAllActions` 在创建敌方前按 `_enemy == null` 隐藏面板，`ShowPreparation` 创建敌方后仅刷新内容，没有更新面板可见性。 | 移除 HideAllActions；MatchPageViewModel 同时给出敌方快照与 EnemyVisible，由 Render 设置面板可见性；真实 Playtest 场景的怪物/PvP 准备检查通过。 | Fixed |

| BUG-033 | 2026-09-30 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / RefreshAndStaleOffers | 新增旧报价回归检查失败。 | 测试误用仅三件可售商品的小型商店，按规则不可刷新，却假定刷新成功后报价批次已改变。 | 改用有四件可售商品的中型商店，并显式确认报价批次已变化后再验证旧商品操作。 | Fixed |
| BUG-034 | 2026-09-30 | `scripts/Application/Match/MatchSnapshot.cs` / CardSnapshot.Value | 卡牌快照显示基础价值，出售用例按含 Modifier 的最终价值结算，存在显示与回补不一致。 | 快照使用 GetBaseValue(Value)，经济用例使用 GetFinalValue(Value)。 | 快照改为读取最终价值；Godot 验证加成前后快照隔离及实际出售金额与显示值一致。 | Fixed |

| BUG-035 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / RenderedScene | P2 场景迁移后，真实场景验证因找不到旧节点或转换为 GridContainer 失败。 | 回归检查硬编码了旧场景路径与逐格按钮类型。 | 改为读取 MatchShell 的动作和独立 CardItemView；补验节点复用、共列布局及详情限位；104项通过。 | Fixed |
| BUG-036 | 2026-10-01 | `scripts/Presentation/Playtest/MatchShell.cs` / 商品显示 | 初次迁移商品操作时只显示购买价格，遗漏卡名与卡面概要。 | 直接使用 UiAction.Text，未组合只读 CardSnapshot 的显示字段。 | 恢复共用 FormatCardFace 与完整详情 tooltip；购买事件继续携带同次报价版本。 | Fixed |
| BUG-037 | 2026-10-01 | `scripts/Presentation/CardFace/CardFace.cs` / Title | 组件展示的长名称越过卡面边界，覆盖邻近区域。 | Title 没有启用裁切和超长文本省略。 | 启用 ClipText 与 TrimEllipsis；完整名称保留在 tooltip/详情，1280×720与1920×1080实际渲染图确认长名不越界。 | Fixed |
| BUG-038 | 2026-10-01 | `scripts/Presentation/Playtest/MatchShell.cs` / LeavePanel | 新增展示入口后，多项右侧操作同时可见时，VBox 最小高度可能超过 ContextRow。 | 直接给 VBox 设尺寸不能压缩子按钮累计最小高度。 | LeavePanel 放入固定行高的 LeaveScroll，横向禁滚、纵向按需滚动；场景回归105项通过。 | Fixed |
| BUG-039 | 2026-10-01 | `scripts/Presentation/PhaseOneVerification.cs` / 原生输入验证 | 延迟执行 async lambda 时出现 Task 不能转换为 Variant 的运行错误。 | Callable.From 选中了返回结果的泛型重载。 | 显式转换为 Action 使用 async void 回调，并捕获验证异常以退出码报告失败；原生验证通过。 | Fixed |
| BUG-040 | 2026-10-01 | `scripts/Presentation/Playtest/CardItemView.cs` / SelectionOutline | 展示页没有调用 SetSelected 时，所有卡面被默认面板背景压暗。 | 新建轮廓 Panel 默认可见，尚未替换默认样式。 | 轮廓初始隐藏，仅选中或焦点时显示透明底边框；实际图确认卡面亮度正常。 | Fixed |
| BUG-041 | 2026-10-01 | `scripts/Presentation/Playtest/CardItemView.cs` / _GetDragData | 原生输入拖拽不能开始。 | Godot 传入按下位置，拿该位置与按下位置计算阈值永远得到0。 | 原生 _GetDragData 直接生成身份载荷；显式8px阈值由 MouseMotion 检查并 ForceDrag，避免重复启动；原生输入验证通过。 | Fixed |
| BUG-042 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / NativeDrag | Headless主窗口合成鼠标输入时，拖放预览读取到(0,0)，无法提交。 | 测试把主窗口系统鼠标位置与合成事件位置混用，并遗漏鼠标进入通知。 | 使用独立SubViewport、NotifyMouseEntered和本地PushInput驱动原生GUI；Escape同步输入动作状态；阈值/跨区/释放一次/取消均通过。 | Fixed |
| BUG-043 | 2026-10-01 | `scripts/Presentation/CardFace/CardFace.cs` / LayoutCompact | 小型紧凑卡面两侧角饰重叠，遮住名称与数值。 | NinePatch角饰仍保留原生40px边缘，而卡面宽度可能不足80px。 | 角饰保留原生采样尺寸单独缩放，文字与价值提高绘制层级；双分辨率实际图确认无覆盖。 | Fixed |
| BUG-044 | 2026-10-01 | `scripts/Presentation/CardFace/CardValueBadge.cs` / 紧凑价值 | 小型卡牌的持有价值贴着右侧边框，单个数字难以辨认。 | 价值文字右对齐到紧凑卡面的角饰区域。 | 紧凑状态改为紧邻金币图标左对齐，文字13px以上；实际图中1/2/12持有价值均可辨认。 | Fixed |
| BUG-045 | 2026-10-01 | `scripts/Presentation/Playtest/MatchPresenter.cs` / BuyCard | 购买与放置分步编排，合并到尚未放置的库存目标时，该目标仍游离在棋盘之外。 | 界面只对WasCreated结果放置，合并预检也未考虑消耗卡牌释放的空间。 | 应用层BuyAndPlace/AcquireAndPlace统一事务，预检纳入消耗卡牌释放位置；连续合并后目标放置及满盘失败保留检查通过。 | Fixed |
| BUG-046 | 2026-10-01 | `scripts/Presentation/Playtest/ComponentShowcase.cs` / _ExitTree | P4截图模式退出时报告不存在的Resized连接。 | 截图分支提前返回，未建立普通展示模式的Resized订阅，退出仍无条件解除。 | 仅普通展示模式解除对应连接；双分辨率截图进程不再报告断连错误。 | Fixed |
| BUG-047 | 2026-10-01 | `scripts/Presentation/Playtest/CardDetailsView.cs` / HeroDetailsView | 出售和奖励浮层背景过于透明，文字与底下棋盘/商品混在一起。 | 默认主题Panel带透明背景，缺少不透明浮层底色。 | 两个浮层设置不透明底色、边框与内边距，双分辨率实际渲染图确认文字独立可读。 | Fixed |
| BUG-048 | 2026-10-01 | `scripts/Presentation/Playtest/ShopView.cs` / LayoutView | 第一版商品卡面高于上排滚动区域，初始位置看不到卡面底部价值。 | 商品使用固定140px高度，没有按内容行可用高度重排。 | 商店卡面高度随滚动区域变化，保持三种尺寸比例；1280×720/1920×1080截图确认完整卡面可见。 | Fixed |
| BUG-049 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / TransactionScene | 新增真实出售按钮验证找不到VBoxContainer/Sell节点。 | 测试假定动态节点使用类型名，Godot实际生成带序号的名称。 | 浮层内容容器显式命名Content，验证使用稳定路径；114项回归通过。 | Fixed |
| BUG-050 | 2026-10-01 | `scripts/Presentation/Playtest/HeroDetailsView.cs` / ClaimRequested | 对局结束后奖励列表误禁用领取，与原奖励用例及右上领取入口不一致。 | 用棋盘移动权限BoardEnabled代替奖励领取权限，并在新协调入口追加终局禁令。 | 奖励按钮读取Reward.Enabled，领取遵循原应用用例；棋盘仍只读，移除额外终局限制。 | Fixed |
| BUG-051 | 2026-10-01 | `scripts/Presentation/Playtest/ComponentShowcase.cs` / CaptureCardPool | 全卡池截图的右列与下排超出画面，无法看到全部17张卡牌。 | 截图网格使用1920×1080布局，但窗口仍按项目默认逻辑尺寸拉伸，网格被再次放大。 | 仅全卡池截图模式将窗口逻辑尺寸设为1920×1080，等待布局刷新后再创建网格和截取；重新核对全部17张卡面。 | Fixed |
| BUG-052 | 2026-10-01 | `docs/design/SkillDataTable.csv.import` / Godot 编辑器资源扫描 | 导入原画时编辑器报告技能 CSV 与生成的名称翻译资源 UID 重复。 | 正式内容 CSV 错用翻译导入器，将业务字段当作语言列生成翻译资源，其中技能名称翻译资源与源表共用 UID。 | 4份正式 CSV 改用保留原文件的 keep 导入模式，清理35个没有消费者的翻译产物；编辑器重新导入无UID重复且不再生成翻译资源，CSV原文与参考区哈希不变，构建0警告0错误，Godot115/115验证通过。 | Fixed |
| BUG-053 | 2026-10-01 | `scripts/Presentation/Playtest/BattlePlaybackPresenter.cs` / `ComponentShowcase.cs` | P5 初次构建的播放游标及截图夹具类型引用不匹配。 | Tick 使用 long、秒数使用 decimal，新入口误用 int 和不正确的枚举命名空间。 | 游标统一 long，播放时钟显式转换 decimal 为 double；修正 SideId 与 CardAcquisitionSource 的命名空间。构建0警告0错误。 | Fixed |
| BUG-054 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` | 新增播放页后两项旧场景回归失败。 | 旧流程假定战斗按钮立即进入结果，页面互斥夹具未给播放页敌方数据。 | 真实场景检查先验证播放页与暂停/倍速按钮，再跳过；更新枚举页夹具，补验冻结结算不被重复操作修改。117/117通过。 | Fixed |
| BUG-055 | 2026-10-01 | `scripts/Presentation/Playtest/LeavePanel.cs` / `CardItemView.cs` | P5 首次截图中跳过按钮需向下滚动，冷却状态文字靠近效果数值。 | 开发入口排在播放控制之前，状态浮层按底部固定偏移放置。 | 播放控制置前并隐藏开发入口；状态放到插画区域，完整并存状态加入提示；1280×720和1920×1080重新截图确认。 | Fixed |
| BUG-056 | 2026-10-01 | scripts/Presentation/CardFace/CardFace.cs / 卡牌插画占比 | 插画只在中部画框显示，紧凑卡面占比还随效果行数改变，无法覆盖整张卡牌。 | 标题、画框和底栏按纵向分区，LayoutCompact从插画区域扣除信息高度。 | 插画及其容器统一铺满卡面，信息和边框置于上层；去掉不透明纸纹，使用半透明底板与文字描边。基准三尺寸、全17卡池及1280棋盘截图已核对，117/117通过。 | Fixed |
| BUG-057 | 2026-10-01 | scripts/Presentation/CardFace/CardFace.cs / 基准卡面叠加顺序 | 400高基准卡面的效果和价值被金色边框覆盖。 | 信息ZIndex仅在紧凑布局设置，基准布局信息与装饰同层，底部间距不足。 | 在组件初始化统一抬高标题/效果/价值层级；为基准效果及价值保留安全间距，三尺寸原生截图核对可读。 | Fixed |
| BUG-058 | 2026-10-01 | `scripts/Presentation/CardFace/CardDisplayAdapter.cs` / 卡牌详情 | 兽皮、钻石的获得效果及珠宝袋、百宝箱的出售效果未显示；狮鹫的事件触发效果显示为泛化被动光环。 | 详情只按战斗能力枚举拼接文本，正式描述未进入属性与快照，无法完整表达获得、出售及事件触发条件。 | 增加只读 Description 属性，同步17张正式卡牌与CSV，经实例/商店/对局快照传给详情；按效果语义整理发动、回响、光环。构建通过、118/118验证通过，三类描述原生截图核对无裁切。 | Fixed |
| BUG-059 | 2026-10-01 | `scripts/Presentation/Playtest/PlaytestVerification.cs` | 新增莫娜后6项依赖帕拉帝恩卡池的验证失败。 | 用例按列表下标选择英雄，按key排序后第一位变为尚无专属卡牌的莫娜。 | 相关验证显式选择 `hero.paladin`，消除英雄排序依赖。 | Fixed |
| BUG-060 | 2026-10-02 | `scripts/Presentation/CardFace/CardDisplayAdapter.cs` / Details | 大教堂、狮鹫、铁匠铺等卡牌显示无关的攻击0，无发动能力的卡牌仍显示冷却占位值。 | 详情无条件遍历 CurrentValues，没有根据能力判断属性用途；内容中的占位字段被当作有效展示属性。 | 仅有 Active 能力时显示冷却，仅伤害效果读取攻击属性时显示攻击；保留真实零值攻击，不修改属性或战斗规则。新增辅助卡、材料卡、零攻击及护甲公式回归检查。 | Fixed |
| BUG-061 | 2026-10-02 | `scripts/Presentation/PhaseOneVerification.cs` / 拖拽出售检查注册 | 新增场景验证首次构建报 CS0027。 | 在字段初始化表达式中用 lambda 捕获 this，C# 不允许此处引用实例。 | 按现有场景验证方式在 _Ready 中注册检查。 | Fixed |
| BUG-062 | 2026-10-02 | `scripts/Domain/Combat/AbilityDefinitions.cs` / `BattleStateRecorder.cs` | 布尔状态接入首次构建失败。 | 新增状态key校验缺少 Common 引用，快照对象初始化前多保留一个 Select 闭合括号。 | 补齐引用并修正构造／初始化表达式，构建0警告0错误。 | Fixed |
| BUG-063 | 2026-10-02 | `scripts/Presentation/Verification/CardStateChecks.cs` / BerserkActiveOnly | 狂暴被动与周期回归首次失败。 | 夹具忽略当前多重也重复战斗开始被动，低估初始伤害与中毒量。 | 保留现行多重规则，按两次未加成被动及14点周期中毒核对，并另验回响不加成。 | Fixed |
| BUG-064 | 2026-10-02 | `scripts/Content/Cards/HolyGriffinCardDefinition.cs` / 发动描述 | 神圣狮鹫疾速文案限定人类，但实际作用于全部直接相邻己方卡牌。 | 文案未与通用相邻效果的实际筛选规则一致。 | 按确认的玩法保留全部相邻己方目标，同步定义与CSV描述，增加非人类邻居验证。 | Fixed |
| BUG-065 | 2026-10-02 | `scripts/Presentation/Verification/SmallRedPotionChecks.cs` | 红药水验证首次构建失败。 | 发动事件来源字段误写为SourceId，实际字段为SourceCardId。 | 修正事件字段引用并重新构建与运行验证。 | Fixed |
| BUG-066 | 2026-10-02 | `scripts/Application/Combat/BattleSetupFactory.cs` / 任务冷却 | 冷却属性任务加成改变展示属性，但原主动能力仍使用定义冷却。 | 冻结战斗输入时未把有效冷却属性差值传入主动能力。 | 使用通用CardAbilityComposer合成任务效果与有效冷却，验证原木法杖任务后首发为5秒且展示一致。 | Fixed |
| BUG-067 | 2026-10-02 | `scripts/Presentation/Verification/LogStaffChecks.cs` | 原木法杖验证首次构建失败。 | 伤害事件数值字段误写为Amount，实际为RawDamage。 | 修正引用，构建与143项回归通过。 | Fixed |
| BUG-068 | 2026-10-03 | `scripts/Presentation/Verification/CombatChecks.cs` / 状态周期验证 | 新状态周期验证首次构建失败。 | 实际事件Tick为long，预期元组数组推断为int，SequenceEqual类型不匹配。 | 显式声明预期元组Tick为long，构建通过。 | Fixed |
| BUG-069 | 2026-10-03 | `scripts/Presentation/Playtest/MatchPresenter.cs` / 怪物奖励提示 | 引入经验升级后，跨升级阈值的怪物奖励可能显示负经验。 | 原提示直接用结算前后的经验余数相减，忽略升级消耗。 | 提示将等级增加折算为10经验后再计算本次获得量，实际经验由统一入口升级并保留余数。 | Fixed |
| BUG-070 | 2026-10-03 | `scripts/Domain/Combat/BattleStatusResolver.cs` / 日蚀伤害 | 日蚀伤害绕过护甲，与确认的护甲优先规则不符。 | 日蚀调用通用伤害结算时将 BypassArmor 设为 true。 | 双方日蚀伤害均改为 BypassArmor=false，沿用伤害曲线、频率和日蚀来源标记；同步玩法及术语文档。 | Fixed（未运行对战验证） |
| BUG-071 | 2026-10-03 | `project.godot` / 游戏窗口显示 | 用户报告运行游戏时肉眼可见黑色闪烁，电脑录屏未捕获；关闭HDR后仍发生，用户确认未启用G-Sync。 | 用户确认切换Vulkan后不再闪烁，问题与当前环境的DX12渲染路径相关；具体驱动或显示链路根因未确认。 | 保持HDR输出关闭，Windows渲染驱动使用Vulkan；用户重启Godot运行后确认不再闪烁。 | Fixed（用户确认） |
| BUG-072 | 2026-10-03 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / 插画验证 | 未配置插画的合法卡牌无法通过验证，尽管卡面已有缺图占位。 | 验证强制要求每张卡牌配置正式原画，未覆盖空插画标识的展示契约。 | 空插画验证占位纹理与身份传递；已配置原画继续校验资源路径和尺寸。 | Fixed |
| BUG-073 | 2026-10-03 | `scripts/Domain/Combat/AbilityDefinitions.cs` / 冷却光环配置验证 | 新机制首次构建失败。 | 引用了不存在的GameElements.IsKnown方法。 | 改为沿用效果定义的非空元素key校验，元素身份仍由正式卡牌定义验证。 | Fixed |
| BUG-074 | 2026-10-03 | `scripts/Presentation/Playtest/KeyedActionView.cs` / 英雄缩略图初版 | 初版构建失败。 | 将Godot的icon_max_width主题常量误用为Button属性。 | 修正后按用户要求将英雄选角改为独立原画浏览组件，不再使用按钮图标。 | Fixed |
| BUG-075 | 2026-10-03 | `scripts/Presentation/Playtest/PlayerHeroPanel.cs` / 姓名布局 | 原画接入后英雄姓名未在截图中显示。 | 自动换行且裁切的Label在HBox内未得到有效最小高度。 | 为姓名设置48像素最小高度及垂直居中，1280×720截图确认恢复显示。 | Fixed |
| BUG-076 | 2026-10-04 | `scripts/Application/Economy/ShopCardPoolService.cs` / 商品等级筛选 | 商店首批商品及刷新可能出现高于商店等级的卡牌。 | 可售池只按英雄归属和尺寸过滤，报价直接采用卡牌初始等级，没有检查商店等级上限。 | 共用可售池增加初始等级≤商店等级条件，首批单件、多件及刷新统一使用过滤后的池；刷新资格按合格候选数判断，同步规则及边界/空池回归。 | Fixed |
| BUG-077 | 2026-10-04 | `scripts/Presentation/Playtest/ComponentShowcase.cs` / 遭遇截图导出 | 新遭遇截图中原画与晶体比源图明显偏暗。 | HDR 2D 视口读回线性色彩，直接保存为普通 PNG，没有转换到 sRGB。 | 遭遇截图保存前按 HDR 视口转换像素到 sRGB，再导出 RGBA8 PNG；复查两档分辨率。其他历史截图入口另行统一。 | Fixed |
| BUG-078 | 2026-10-04 | `scripts/Presentation/Playtest/LeavePanel.cs` / `PlayerHeroPanel.cs` | 侧栏布局初次截图中阶段按钮只剩边框，英雄姓名被压缩。 | 横向容器内启用文字裁切，控件最小宽度不再由文字提供。 | 阶段按钮按实际字体测量设最小宽度，英雄姓名保留120px最小宽度；多档窗口及大窗缩回小窗检查姓名、按钮和购买区可见性。 | Fixed |
| BUG-079 | 2026-10-04 | `scripts/Presentation/Playtest/ComponentShowcase.cs` / CaptureEncounters | 新增回放截图初版实际仍显示普通回合商店。 | 截图流程假定选择下一回合首个候选一定进入战斗，未核对页面类型。 | 按正式排程推进并检查Preparation后再启动播放，重建并检查战斗准备与回放截图。 | Fixed |
| BUG-080 | 2026-10-04 | `scripts/Presentation/Verification/StandardAuraChecks.cs` | 旗帜回归初版不能编译。 | 将CardBattleSnapshot的Values误写为CombatValues。 | 使用实际Values字段读取冻结属性，并在构建成功后运行回归。 | Fixed |
| BUG-081 | 2026-10-04 | `scripts/Presentation/Verification/StandardAuraChecks.cs` / Battle夹具 | 旗帜治疗断言比预期少1生命。 | 夹具默认在最后一刻触发日蚀，混入无关伤害。 | 日蚀起点设到夹具超时之后，独立验证光环效果；正式日蚀规则及战斗基线不变。 | Fixed |
| BUG-082 | 2026-10-04 | `scripts/Domain/Combat/BattleEffectResolver.cs` / AlliedAttributeAuras | 新光环初版给无对应属性能力的卡牌也显示加成，与属性修改规则不一致。 | 光环只检查所属方与存活/战场状态，没有检查目标支持的属性。 | 按SupportsCombatAttribute筛选目标，治疗效果注册治疗加成支持；使用有攻击能力的光环来源验证包含自身，召唤和叠加验证继续保留。 | Fixed |
| BUG-083 | 2026-10-04 | `scripts/Presentation/Verification/StandardAuraChecks.cs` / 旗帜手验证 | 新验证首次构建失败。 | 将属性变化事件Amount误写为Delta，且Tick预期数组使用int。 | 改用Amount及long数组，与领域事件契约一致。 | Fixed |
| BUG-084 | 2026-10-04 | `scripts/Presentation/Verification/OrderPlateArmorChecks.cs` | 秩序板甲验证首次构建失败。 | 缺少Domain.Definitions命名空间，无法解析尺寸、元素与标签。 | 补充using，构建和167项回归通过。 | Fixed |
| BUG-085 | 2026-10-08 | `.codex/skills/project-star-dev-git/SKILL.md` / 拉取完成判定 | 本地 HEAD 与缓存 origin/dev 同为4c2538a时误报已同步，直接查询服务器后发现远端为e7da243、本地落后22个提交。 | 完成判定只比较本地引用，缺少服务器分支SHA的独立核验；首次fetch未反映最新状态的具体原因未确认。 | 拉取最终必须使用ls-remote查询服务器并核对服务器、缓存远端与HEAD三个完整SHA；不一致时有限重试，失败或仍不一致时禁止报告已同步；推送后同样直接核验。同步修正skill中的过期项目路径。 | Fixed（工作流规则已更新） |
| BUG-086 | 2026-10-08 | `scripts/Presentation/CardFace/CardDisplayAdapter.cs` / 元素倍率说明 | 新增说明初稿引用不存在的 `GameElements.DisplayName`，源码检查时发现会阻止构建。 | 将元素合法性定义误当作展示名称接口。 | 在表现层格式化已知元素的中文名；构建0警告0错误，184/184回归通过，说明验证包含光属性及恶魔/亡灵条件。 | Fixed |
| BUG-087 | 2026-10-08 | `BoardZoneView.cs` / `MatchShell.cs` / 棋盘比例 | 卡面在占格按钮内留有空隙，窗口越宽越容易卡格不贴合。 | 骨架分别分配段高和宽度，格高取剩余段高，卡面再按比例居中缩小。 | 骨架以格宽和视口高度共同约束三段；统一格高为两倍格宽，卡面覆盖完整占格矩形，落点和预览使用同一边界。 | Fixed |
| BUG-088 | 2026-10-08 | `CardFace.cs` / 上下信息栏 | 不同效果数量改变底栏高度，且上下栏不等高。 | 底栏按纵向效果行数增高，顶栏独立计算。 | 共用同一栏高，效果横向排列；紧凑卡面优先展示能读清的效果，其余由提示和详情完整承载。 | Fixed |
| BUG-089 | 2026-10-08 | `PlaytestVerification.cs` / 重开夹具 | 布局首轮回归183/184，场景重开检查失败。 | 重开已移入开发折叠区，夹具仍直接触发隐藏按钮，被正常输入门控拒绝。 | 夹具先打开开发工具再点击重开，保留隐藏按钮不执行的门控。 | Fixed |
| BUG-090 | 2026-10-08 | `ShopView.cs` / 商品滚动条 | 首次实际截图出现多余竖滚动条。 | 商品行最小高度与容器测量造成非必要纵向溢出。 | 商品统一在固定阶段内布局，关闭纵向滚动，单独预留购买、失败原因和反馈空间。 | Fixed |
| BUG-091 | 2026-10-08 | `ResultView.cs` / 奖励图卡 | 结算图卡初次构建失败。 | 新组件遗漏卡面适配器所在命名空间。 | 补充Presentation.CardFace引用并重新构建。 | Fixed |
| BUG-092 | 2026-10-08 | `BoardZoneView.cs` / `PlaytestVerification.cs` / 小窗几何 | 几何和窗口验证因约0.00003px的负坐标或零点比较失败。 | 非整数格宽乘回总宽产生浮点误差，零坐标断言比其他边界断言严格。 | 格位左边界钳制到0，等尺寸卡面归零定位，几何检查使用0.1px容差，并继续检查四档窗口及缩回小窗。 | Fixed |
| BUG-093 | 2026-10-08 | `ShopView.cs` / `ResultView.cs` / 大窗缩回 | 从2560×1440切换到2560×1080时购买按钮超出阶段区，结算奖励容器也使用同一测量模式。 | Disabled滚动容器把旧商品行高度计入自身最小高度，内容又读取容器高度，形成不能缩小的测量循环。 | 两处改用ShowNever，隐藏竖滚动条但允许视口独立缩小，商品行和奖励行按阶段可用高度布局；复验大窗缩回与全部购买按钮基线。 | Fixed |
| BUG-094 | 2026-10-08 | `PlaytestVerification.cs` / 结算图卡领取夹具 | 新增结算图卡领取检查后184/185回归失败。 | 夹具主动关闭奖励浮层，仍用原断言要求浮层保持可见。 | 改为检查关闭状态不被领取刷新改变，并保留重复旧按钮不会再次领奖的验证。 | Fixed |
| BUG-095 | 2026-10-08 | `ShopView.cs` / 三尺寸截图 | 大型商品未显示合并箭头，只有小中型显示。 | 元信息侧栏的宽度门槛过大，没有为大型商品预留固定可读空间。 | 同高商品按最大尺寸预留76px侧栏预算，三尺寸均展示冷却及合并目标等级，保持购买按钮同基线。 | Fixed |
| BUG-096 | 2026-10-08 | `MatchPresenter.cs` / `TopBar.cs` / 当前回合标记 | 第1回合商店标成第2回合，第4回合怪物战标成第5回合；第8回合可能提前显示下一轮。 | 选择遭遇时领域已推进排程，顶栏直接展示最新领域轮回合，没有保留当前访问身份。 | 页面模型捕获进入遭遇前的轮回合，顶栏在访问和结算期间保持该标记；返回选择后显示下一回合，不修改领域排程。回归覆盖商店、怪物战及第8回合跨轮。 | Fixed |
| BUG-097 | 2026-10-08 | `scripts/Application/Match/MatchSnapshot.cs` / `CopyAbilities` | 新增传动齿轮后，卡面、插画及词条快照的三项回归抛出相邻回响配置异常。 | 能力快照重建遗漏新增的 `TriggerCardSide`，带方向的回响在复制时丢失必要配置。 | 补齐触发方向的冻结传递；分级验证使用复制后的能力执行战斗，全部内容身份、插画及词条快照回归通过，189/189全量通过。 | Fixed |
| BUG-098 | 2026-10-08 | `scripts/Presentation/Playtest/BattlePlaybackPresenter.cs` / `Project` | 临时转变出的登神者解锁元素任务后，Runtime已改为光属性，回放仍显示转变时的无属性并清空任务。 | 整卡转变投影提前返回，未继续应用新卡自身的冻结任务身份与进度。 | 转变投影继续读取冻结任务数据；新增达到60次阈值后显示光属性与任务进度、原对局卡仍为无属性且不累计临时成长的验证。构建0警告0错误，194/194全量回归通过。 | Fixed |
| BUG-099 | 2026-10-08 | `scripts/Application/Economy/CardEconomyService.cs` / `BuildMergePlan` | 连续合并后登神者任务看似重置，原高等级卡的永久成长、任务身份与宝石一并丢失。 | 合并链始终保留首个低等级目标，把已培养的高等级实例作为材料移除；此前任务验证只覆盖直接升级，未覆盖实际连续合并。 | 按用户确认每步保留原有更高等级卡，最终升级链中最高等级实例；补充正式购买路径的未完成/已解锁任务、永久成长、身份、位置和后续发动验证，同步宝石及满盘/未放置目标验证。构建0警告0错误，195/195全量回归通过。 | Fixed |
| BUG-100 | 2026-10-08 | `scripts/Presentation/Verification/AscendantChecks.cs` / `MergePreservesProgress` | 连续合并验证初版构建失败，随后零值多重读取及位置断言失败。 | 误用不存在的发动事件；假定属性快照必含零值 `Multicast`；把放入材料前的位置误当作合并前的位置，忽略正常棋盘推挤。 | 改用 `AbilityActivatedEvent.SourceCardId` 并排除回响；多重读取使用零值默认；在材料放置完成后捕获位置。 | Fixed |
| BUG-101 | 2026-10-08 | `scripts/Presentation/Verification/CardCatalogChecks.cs` / `Interaction` | 图鉴首次场景验证找不到卡牌列表节点。 | 验证路径假定未命名控件会使用类型名，Godot实际生成自动节点名。 | 浏览区显式命名Browser，验证使用稳定节点路径。 | Fixed |
| BUG-102 | 2026-10-08 | `scripts/Presentation/Verification/CardCatalogChecks.cs` / 搜索夹具 | 搜索验证与分尺寸截图未刷新列表，图片仍为全卡池。 | 夹具只直接设置LineEdit.Text，程序赋值不会发出用户输入的TextChanged信号。 | 设置文字后显式发送TextChanged，检查过滤结果与详情，并重新截图。 | Fixed |
| BUG-103 | 2026-10-09 | `scripts/Presentation/Verification/JadeToadChecks.cs` / 标签断言 | 验证初稿假定尺寸标签排在野兽标签前，源码核对发现断言会误判合法定义。 | 将无序标签集合按数组顺序比较。 | 改为检查标签数量及集合成员，不依赖枚举顺序；构建0警告0错误，209/209回归通过。 | Fixed |
| BUG-104 | 2026-10-09 | `scripts/Presentation/Playtest/HeroSelectionView.cs` / 名册缩略图 | 首次实际截图中英雄缩略图细节出现明显锯齿。 | 原画未生成mipmap，直接从1254像素缩至52像素显示，缺少足够的缩小预滤波。 | 表现层用Lanczos生成104像素缩略纹理并按插画key缓存，完整原画继续使用原资源；1280×720实际截图复查细节，四档窗口及缩回通过。 | Fixed |
| BUG-105 | 2026-10-09 | `scripts/Presentation/Playtest/CardCatalogView.cs` / 分类筛选初稿 | 源码核对发现初稿包含不存在的尺寸标签辅助方法，归属缓存局部变量与循环变量同名，会阻止构建。 | 未按实际接口名FromSize调用，并在同一作用域重复声明faction。 | 改用GameTags.FromSize，缓存变量命名为previousFaction；构建0警告0错误，209/209回归及两档实际图鉴窗口检查通过。 | Fixed |
| BUG-106 | 2026-10-09 | `scripts/Presentation/Verification/DuskSongJungleChecks.cs` / 流程夹具 | 首轮构建因不存在的Options字段失败，源码核对同时发现OptionsRevision误名。 | 未按实际页面模型的EventOptions/EventRevision字段读取选项及版本。 | 修正字段引用，构建0警告0错误，214/214回归通过，包含怪物战转场及回合保持检查。 | Fixed |
| BUG-107 | 2026-10-09 | `PlaytestText.cs` / `KeyedActionView.cs` / 加权事件说明 | 1280×720首张实际截图只显示砍伐的植物分支，50%怪物风险被挤到滚动区下方。 | 每个分支重复完整奖励句式并各占一行，默认16px按钮文本超过阶段区高度。 | 加权结果使用简洁说明，每行最多两个分支，多行按钮13px；实际截图确认两项及三种概率首次显示即可读全，原生点击和两档窗口通过。 | Fixed |
| BUG-108 | 2026-10-09 | `PlaytestVerification.cs` / `TestEncounterLevels` | 调整4级遭遇的验证初稿构建失败。 | 错误地从没有Level属性的EncounterDefinition基类读取等级。 | 保留原有普通排程隔离检查，断言当前正式候选未被统一覆盖为4级。 | Fixed |
| BUG-109 | 2026-10-09 | `scripts/Presentation/Verification/MagicCauldronChecks.cs` / `ThresholdAndMerge` | 魔法坩埚任务回归在快照冷却检查处抛出多元素异常。 | 任务解锁后快照同时包含主动施毒和被动减冷却能力，验证错误地假定仅有一个能力。 | 按Active筛选主动能力后检查5秒冷却，保留任务完成、合并与区域迁移验证；构建0警告0错误，217/217全量回归通过。 | Fixed |
| BUG-110 | 2026-10-09 | `SilverNeedleGrassChecks.cs` / 治疗验证夹具 | 初稿构建因战斗开始枚举名不存在而失败，后续临时诊断字符串也有转义错误。 | 错用OnBattleStart，并误对插值表达式内部引号转义。 | 改用PassiveOnBattleStart并移除临时诊断，保留实际治疗和冻结输入检查。 | Fixed |
| BUG-111 | 2026-10-09 | `SilverNeedleGrassChecks.cs` / 卡面治疗断言 | 首轮回归误判银针草出售后的治疗显示。 | 正式卡详情保留分级描述，验证却假定详情内含动态治疗句式。 | 改为校验FaceEffects的实际治疗数值，同时保留战斗治疗和冻结快照断言。 | Fixed |
| BUG-112 | 2026-10-09 | `BattleRuntime.cs` / CardBattleState治疗属性初始化 | 银针草出售后卡面显示治疗增加，但实际战斗仍使用原治疗量。 | 初始化治疗能力只登记HealingBonus为支持属性，未读取冻结CombatValues中的永久贡献。 | 从战斗输入复制HealingBonus，验证各等级实际治疗提升、旧输入不变和升级保留。 | Fixed |
| BUG-113 | 2026-10-09 | `BattleEffectResolver.cs` / 治疗上限 | 极大但合法的治疗量在满血上限裁定之前可能抛出整数溢出。 | 当前生命与治疗量以checked int相加，随后才取最大生命上限。 | 先用long累加并限制到最大生命，再转回int；验证合法int上限治疗将生命补至上限。 | Fixed |
| BUG-114 | 2026-10-09 | `BattleRuntime.cs` / 固定回放基线 | 永久治疗修复初版使无治疗加成的旧战斗记录出现额外零值属性键。 | 初始化对未配置HealingBonus的输入也写入0，改变了冻结状态字典。 | 仅复制输入中实际配置的HealingBonus，保留旧输入的字段集合及固定基线。 | Fixed |
| BUG-115 | 2026-10-09 | `scripts/Presentation/Verification/WhetstoneChecks.cs` / 快照入口 | 磨刀石验证初稿因引用不存在的快照工厂而构建失败。 | 未按现行MatchSnapshot.From入口获取快照。 | 改为MatchSnapshot.From，构建0警告0错误，磨刀石3项行为验证通过。 | Fixed |
| BUG-116 | 2026-10-09 | `scripts/Presentation/Verification/JailbreakerChecks.cs` / 奖励初值断言 | 越狱者奖励验证初稿把新局金币与经验视为零，误判正常奖励结算。 | CreateMatchService创建时已发放首轮收入和首回合经验，验证未计入这些初值。 | 记录战前资源，断言胜利后金币增加3、经验增加2，并保留奖励池等级与真实领取检查。 | Fixed |
| BUG-117 | 2026-10-09 | `scripts/Presentation/CardFace/CardDisplayAdapter.cs` / 随机持续状态技能说明 | 放逐说明会显示内部ImmobilizeDuration枚举和敌方英雄目标标题，无法正确说明双方随机卡牌禁锢。 | 随机卡牌效果直接插值状态枚举；能力标题仅读取通用Target字段，未反映组合效果的双方卡牌目标。 | 随机持续状态使用中文名称并显示目标数量；同时包含己方与敌方随机卡牌效果的能力标题显示双方战场卡牌，验证各等级说明；230/230回归通过。 | Fixed |
| BUG-118 | 2026-10-09 | `scripts/Presentation/Verification/BanishChecks.cs` / 命名空间 | 放逐验证初稿因缺少GameFactions所在命名空间而构建失败。 | 验证文件遗漏Domain.Definitions引用。 | 添加对应using；构建0警告0错误，230/230行为回归通过。 | Fixed |
| BUG-119 | 2026-10-09 | 小魔女卡牌原画 / 黑犀金龟、小型魔法药水、小型生命药水 | 三张小型卡牌仍引用方形原画，填满1:2卡面时横向裁切主体。 | 历史原画1254×1254与Definition的小型1:2比例不匹配。 | 内置图像工具重绘为887×1774，接入独立v2资源；9张小魔女现用插画比例全部匹配定义，实际卡面主体完整；构建0警告0错误，230/230回归通过。 | Fixed |
| BUG-121 | 2026-10-09 | `scripts/Presentation/Playtest/PaladinPortrait.gdshader` / 圣骑士选角动效 | 用户主要看到光点和亮度变化，人物缺少可见待机动作。 | 原动效仅对布料施加极小局部位移，头部、胸肩与持锤手没有动作，叠加光点成为主要运动。 | 改为轮廓蒙版驱动的呼吸、头颈轻摆、握锤同步运动及延迟布料摆动；随后按用户要求叠加跟随动作的圣光与少量光点。实际渲染验证人物运动、面部不受光效影响、外侧UI稳定及8秒首尾衔接，233/233回归通过。 | Fixed |
| BUG-120 | 2026-10-09 | `scripts/Presentation/PhaseOneVerification.cs` / 技能图鉴验证注册 | 图鉴验证初版构建失败。 | 字段初始化列表中的验证回调引用this，而实例此时尚未构造。 | 把需要真实控件宿主的原画检查注册到_Ready创建的场景集成分类。 | Fixed |
| BUG-122 | 2026-10-09 | HTML对局预览 / 棋盘卡面比例 | 草稿的棋盘宽度随窗口变化，卡高却按视口高度计算，造成小、中、大卡面比例偏离。 | 卡高与十格实际格宽使用不同的尺寸基准。 | 棋盘通过容器宽度及格间距计算统一卡高为两倍单格宽，三种尺寸共用占格规则；1280×720及1920×1080浏览器检查无横向溢出，实际截图已核对。仅涉及HTML预览。 | Fixed |
| BUG-123 | 2026-10-09 | ComponentShowcase.cs / CaptureEncounters 截图色彩 | Compatibility 截图底色、插画及面板发白，不能用于 HTML 对照。 | 仅判断 UseHdr2D，在 Compatibility 已为 sRGB 的读回图上再次做 LinearToSrgb。 | 增加实际渲染方法判断，仅对 Forward+/Mobile HDR 做转换；正式 Forward+ 截图已复查。 | Fixed |
| BUG-124 | 2026-10-09 | ShopView.cs / 宽屏商品购买基线 | 字号放大后不同商品购买按钮纵坐标不同，窗口检查失败。 | 每项单独按禁用原因是否可见预留空间，造成卡高及按钮基线不一致。 | 全商品共享最大原因留白，四档窗口及缩回检查通过。 | Fixed |
| BUG-125 | 2026-10-09 | PlayerHeroPanel.cs / 放大字号后的底栏 | 英雄姓名不显示，奖励按钮换行后覆盖经验并超出小窗。 | 名称行高低于字体真实行高；原按钮边距使三入口超过可用宽度。 | 调整姓名行高、资源条纵坐标、按钮内边距及成长摘要位置；1280×720 实际截图复查姓名和三入口可见。 | Fixed |
| BUG-126 | 2026-10-09 | CardFace.cs / 紧凑小型卡效果 | 放大字体后的1280×720截图中，两位攻击数值10被裁成1。 | 效果区域被右侧价值牌挤压，按近似字符宽度计算的区域不足以容纳图标与两位数。 | 使用真实字体测量；2026-10-10补齐真实2级小型护腕AttackDamage=10、damage图标、文本10与18px字宽／18px可用宽断言，四档原生截图通过。原问题1280×720证据见UI_PARITY_CONTINUE_2026_10_10。 | Fixed |
| BUG-127 | 2026-10-09 | MatchShell.cs / HTML 视觉迁移 | 原生草稿只有大体配色和布局相似，字体、详情结构、间距及响应式效果与获批 HTML 不一致。 | 使用引擎默认字体和旧详情文本框，并以固定窗口高度约束代替 CSS 自然布局，未逐项测量对照。 | 已冻结基准并接入字体、结构化详情和自然滚动骨架；2026-10-10继续调整详情实测样式并修复刷新状态，功能与截图检查已推进。UI_HTML_PARITY_TODO.md记录剩余差异，完整视觉验收未完成。 | Open |
| BUG-128 | 2026-10-09 | MatchShell.cs / HDR 2D半透明UI | 面板、空格和遮罩实际颜色比HTML偏亮，透明度相同仍不一致。 | HDR 2D在线性空间合成透明层，浏览器CSS在sRGB空间合成；单纯修正截图转换无法修正合成后的颜色。 | 对局视口关闭UseHdr2D，截图直接读取sRGB；实际Forward+渲染复查透明底色，构建与234/234回归通过。 | Fixed |
| BUG-129 | 2026-10-09 | CardFace.cs / 等级与元素尺寸 | 设为HTML尺寸后元素徽章仍过大，等级位置也受旧尺寸影响。 | tscn保留的CustomMinimumSize覆盖代码设置的Size。 | 在布局及创建时清除旧最小尺寸，等级外接约35px、元素23px；两档截图复查角部与双元素。 | Fixed |
| BUG-130 | 2026-10-09 | CardDetailsContent.cs / CardDetailsView.cs / 详情尺寸与关闭 | 单段正文末尾被裁切、详情留下约160px空白，关闭按钮被拉伸／遮住；关闭后固定状态未释放，旧延迟回调在节点释放后报错。 | 正文高度按近似字符宽度估算，容器旧Size不会自动收缩；关闭按钮参与PanelContainer布局且TopLevel后绘制层低于面板；关闭只Hide，延迟闭包仍引用已释放控件。 | 按实际段落最小高度设置滚动区，详情及时收缩到内容高度；关闭按钮独立定位并使用高于面板的绘制层，通知MatchShell释放固定状态；移除高度延迟回调并保护定位回调。两档单／多段和属性展开截图复查，234/234回归通过且无运行异常。 | Fixed |
| BUG-131 | 2026-10-09 | ComponentShowcase.cs / HTML截图入口 | 初版截图入口将返回Task的方法直接注册Callable，调用时报Variant转换异常。 | Godot Callable不支持该Task返回值作为Variant。 | 使用async void入口包装等待截图任务；真实Forward+两档截图正常输出。 | Fixed |
| BUG-132 | 2026-10-09 | TopBar.cs / 菱形进度初始绘制 | 首帧菱形位置错误，出现在品牌区附近。 | 首次绘制发生于布局完成之前，后续尺寸更新没有重新绘制缓存。 | 监听ItemRectChanged并重绘；两档实际截图确认八菱形位于回合信息旁。 | Fixed |
| BUG-133 | 2026-10-09 | LeavePanel.cs / 阶段操作小窗 | 窗口检查失败，开发入口宽度被压缩并显示为空。 | 同行操作的最小宽度与主题边距冲突，开发按钮未预留可读宽度。 | 阶段按钮最小宽度40px，刷新／离开排序对应参考；四档窗口／键盘通过。小窗换行和局部滚动的视觉对齐仍属于BUG-127待办。 | Fixed |
| BUG-134 | 2026-10-09 | MatchTheme.cs / 字体与渐变初稿构建 | 初版字体间距和后续渐变字符串构建失败。 | 调用不存在的SystemFont.SetSpacing，且FormattableString缺少System命名空间。 | 使用FontVariation.SpacingGlyph，补充System引用；最终构建0警告0错误。 | Fixed |
| BUG-135 | 2026-10-09 | MatchShell.cs / ToggleEnemy | 展开敌方阵容后，没有对应的返回战斗摘要入口。 | 查看按钮位于BattleStage，展开时整个BattleStage被隐藏；没有在敌方棋盘提供返回动作。 | 敌方阵容内增加表现层“返回战斗摘要”按钮；展开／返回／再次展开检查通过，完整回归234/234。 | Fixed |
| BUG-136 | 2026-10-09 | MatchShell.cs / Render详情刷新 | 回放刷新会隐藏已打开的详情，鼠标未重新进入卡牌时无法恢复悬停提示；右键固定状态也被刷新重置。 | 每次Render直接Hide，并仅用SelectedCardId重置固定状态，没有保存有效悬停／固定卡牌身份。 | CardDetailsView保存当前卡牌身份；同页刷新时复用位置并重渲染新快照，卡牌不存在时关闭；出售／旧按钮连接回归通过，构建0警告0错误。 | Fixed |
| BUG-137 | 2026-10-09 | CardItemView.cs / 结构化战斗详情 | 回放状态说明追加到TooltipText，但新的结构化详情未读取这些说明。 | MatchShell中的_GetTooltip禁用旧提示，CardDetailsContent只收到CardSnapshot，没有接收战斗状态投影。 | 通过只读CardBattleSnapshot显示禁锢／疾速／迟缓／飞行／狂暴／摧毁和任务；2026-10-10补齐有到无状态及任务3/5到5/5解锁断言，两档实际截图确认旧状态移除、进度更新，234/234回归通过。 | Fixed |
| BUG-138 | 2026-10-10 | CardItemView.cs / 等级色卡框的交互轮廓 | 选中和键盘焦点的3px轮廓会覆盖2px等级框；悬停统一蓝色也改变了等级边缘。 | 交互轮廓在卡框原边界内绘制，未区分等级框与外部交互强调。 | 悬停使用等级颜色，选中／焦点轮廓向外扩展，保留等级框；构建、234/234回归及四档窗口／焦点检查通过。 | Fixed |
| BUG-139 | 2026-10-10 | CardDetailsView.cs / CardDetailsContent.cs / MatchShell.ScrollChanged | 同卡刷新让详情每次向右偏移16px、淡入重启并收起实例属性；窗口变化触发滚动后固定详情隐藏，下一次刷新重新打开。 | RefreshCard复用ShowCard导致新展示的偏移／Tween／展开重置；滚动回调无条件隐藏详情。同步复现233/234失败，窗口检查进一步复现隐藏问题。 | 分离新展示与快照刷新，刷新保留位置／Tween／展开及正文高度；同一选中卡复用刷新，旧定位回调按展示版本失效；滚动只隐藏非固定详情。修复后234/234及四档窗口／缩回／键盘检查通过。 | Fixed |
| BUG-140 | 2026-10-10 | PlaytestVerification.WindowsAndKeyboard / 长详情夹具 | 首轮长正文窗口检查在1920×1080误报缺少滚动。 | 8段正文在该窗口能完整容纳，验证错误地要求所有窗口均出现滚动条。 | 使用20段长说明确保四档窗口均超过正文预算，再实际滚动并验证不越界、无主动冷却及短正文收缩；四档窗口／缩回检查通过。 | Fixed |
| BUG-141 | 2026-10-10 | CardItemView._MakeCustomTooltip / MatchShell.ShowHoverCard | 用户截图显示神圣狮鹫和登神者同时出现两份详情，左侧悬停窗重复右侧详情。 | 卡牌同时提供引擎自定义Tooltip和MatchShell统一详情，两条路径均绘制CardDetailsContent。 | 删除卡牌引擎自定义Tooltip并返回空提示，统一通过CardDetailsView悬停／固定／刷新／关闭；商品与奖励也复用同一面板。236/236回归及三档原生截图通过，见UI_CARD_INTERACTION_PLAN.md。 | Fixed |
| BUG-142 | 2026-10-10 | 独立HTML原型 / drawBoards | 静态检查发现原型卡面函数新增动作参数后，棋盘map调用会把数组索引误传为交易动作。 | Array.map直接传入cardHTML，会同时传递索引和原数组。 | 棋盘映射改为显式只传卡牌key的回调，保持棋盘点击为详情；仅修改HTML原型。 | Fixed |
| BUG-143 | 2026-10-10 | 独立HTML原型 / 任务进度预览 | 静态检查发现进度切换按钮展示详情后会立即关闭详情。 | 点击事件继续冒泡到页面空白点击关闭处理器。 | 预览按钮停止冒泡后更新任务进度，保留同一个详情容器；仅修改HTML原型。 | Fixed |
| BUG-144 | 2026-10-10 | 独立HTML原型 / 悬停详情点击命中 | 连续购买检查发现悬停详情覆盖邻近商品，下一次点击未触发购买。 | 未固定的悬停面板也消费鼠标事件，向左避让时覆盖其他卡牌。 | 未固定详情透过鼠标命中卡牌；仅固定详情接收鼠标操作，关闭按钮仍可点。仅修改HTML原型。 | Fixed |
| BUG-145 | 2026-10-10 | ComponentShowcase.cs / 详情事件订阅 | 卡牌详情事件加入邻接定位边界后，组件展示入口构建失败。 | 展示入口仍订阅旧的单参数ShowDetails。 | 同步为卡牌快照与Rect2双参数，并使用ShowNear定位；构建0警告0错误，236/236回归通过。 | Fixed |
| BUG-146 | 2026-10-10 | CardInteractionChecks.Capture / 原生输入夹具 | Escape关闭详情后的购买捕获访问已释放商品节点。 | 取消选择会通过Presenter刷新商店并替换商品控件，夹具继续引用旧节点。 | Escape后重新查询同报价的卡牌节点，再注入鼠标输入；实际鼠标／Enter购买和Space奖励领取通过。 | Fixed |
| BUG-147 | 2026-10-10 | CardDetailsContent / 任务节点名称 | 独立任务区首轮回归无法按任务名称找到节点，234/236通过。 | 直接使用含点号的任务key作为Node.Name，Godot校验节点名称后路径不匹配。 | 节点名称将点号替换为下划线，任务数据关联仍使用原StringName key；236/236通过。 | Fixed |
| BUG-148 | 2026-10-10 | PlaytestVerification.IsolatedComponents / 失效详情断言 | 允许商店详情同页刷新保留后，旧断言仍期待详情关闭，235/236通过。 | 夹具只移除了库存卡牌，该卡牌仍在商品列表中；因此并未真正失效。 | 同时清空商品列表，验证有效报价详情保留与真正移除后关闭；236/236通过。 | Fixed |
| BUG-149 | 2026-10-10 | CardInteractionChecks.Capture / 窄窗证据 | 615×440捕获实际仍为1280×720，并覆盖同名截图。 | 试玩入口设置了1280×720窗口最小尺寸，夹具未解除限制就按请求尺寸检查。 | 仅在截图夹具解除最小尺寸；实际615×440截图复查正文滚动及详情边界，正常试玩最小尺寸不变。 | Fixed |
| BUG-150 | 2026-10-10 | CardDetailsView / 悬停鼠标透过 | 静态复查发现仅忽略面板根节点仍可能让未固定详情遮挡商品点击。 | 外层Content容器及每次Render新建的任务面板仍使用默认MouseFilter，旧的忽略设置未覆盖新子节点。 | 每次刷新后按固定状态递归设置整个Content，关闭按钮保持可点；检查所有新任务节点透过、固定正文接收滚动，236/236通过。 | Fixed |
| BUG-151 | 2026-10-10 | CardInteractionChecks.Capture / 固定任务夹具 | 原生截图复查发现滚动任务后详情消失，旧检查只验证边界仍通过。 | 夹具直接打开详情，没有同步MatchShell的固定状态，卡牌MouseExited将其隐藏。 | 仅在只读视觉快照添加并选中示例卡，检查Visible后再保存；三档任务／蓝色／滚动截图通过，示例不写入正式对局。 | Fixed |
| BUG-152 | 2026-10-10 | ShopView / 窄窗键盘卡牌焦点 | 615×440原生检查中第三张商品仍在可见区域外，键盘焦点移到不可见商品。 | 未开启FollowFocus；启用后聚焦时横向滚动范围从732退为36，商品行动态布局的宽度未显式维持；夹具也曾在布局完成前设置焦点。 | 启用FollowFocus，按可见商品行宽与间距设置内容最小宽度；夹具等待布局再聚焦。三档原生焦点／悬停检查和四档窗口回归通过。 | Fixed |
| BUG-153 | 2026-10-10 | CardItemView / 大型卡牌悬浮复位 | 大型卡牌悬浮后顶到容器顶部，离开后不能恢复居中位置。 | 悬浮Tween将卡面绝对Y设为-6或0，覆盖宽高适配产生的居中偏移；刷新也会与Tween争写位置。 | Tween只更新独立悬浮偏移，LayoutFace统一计算居中位置加偏移；悬浮／离开／刷新／缩放验证、237/237回归、四档窗口和原生拖拽通过。 | Fixed |
| BUG-154 | 2026-10-10 | PlumedHelmetCardDefinition / 描述词条 | 羽饰头盔的相邻卡牌发动充能被标为光环，与术语表和实际回响触发不一致。 | 描述使用CardKeywords.Aura，能力实际为EchoOnAdjacentAlliedCardActivated。 | 已将正式Definition描述改为CardKeywords.Echo；现有分级伤害、相邻人类充能、多重、即时队列与冻结配置检查全部通过，仅更改分类，不改能力。 | Fixed |
| BUG-155 | 2026-10-10 | combat-status.css / HTML状态预览 | 切换到无持续伤害的示例仍显示灼伤／中毒徽章。 | .dot-row的display:flex覆盖浏览器对hidden属性的默认display:none。 | 显式设置.dot-row[hidden]为display:none，浏览器复查正常与非战斗页均隐藏；仅修改HTML设计草案。 | Fixed |
| BUG-156 | 2026-10-10 | combat-status.css / HTML怪物状态区 | 桌面截图中怪物的灼伤／中毒徽章被阶段底边裁切。 | 固定阶段高度容不下新增状态行和内边距。 | 战斗阶段使用自然高度和260px最小高度；宽窄屏检查状态行底边均在阶段边界内，截图复查完整可见。 | Fixed |
| BUG-157 | 2026-10-10 | game-16x9.css / HTML右侧信息栏 | 16:9首轮截图中三排棋盘完整可见，但右侧播放按钮超出画布。 | 怪物资源、英雄资源与阶段操作按自然高度叠加，未控制侧栏总高度。 | 限定942px侧栏，收紧资源／技能间距及肖像高度，压缩反馈文本，保留卡牌比例与字号；1920×1080浏览器实测播放按钮和等级图例均在画布内，截图通过。 | Fixed |
| BUG-158 | 2026-10-10 | game-16x9.js / HTML禁锢示例 | 点击禁锢示例后没有卡牌显示禁锢，野猪冷却继续推进。 | 按固定数组索引选择了没有主动冷却的兽皮，随后被无冷却过滤跳过。 | 禁锢示例明确选择预览野猪实例，不按无冷却卡牌的顺序定位；播放时两次浏览器读取均为3.6秒，禁锢标识可见。 | Fixed |
| BUG-159 | 2026-10-10 | game-16x9.html / 卡面设计范围 | 首版排版草案自行新增卡内名称，并改动卡框、元素、效果／价值位置，超出用户要求。 | 页面草案重新制作卡面，没有复用已有获批样式。 | 移除草案卡内名称和自定义卡框；逐字复用card-interactions.html卡面CSS与原结构，复用combat-status.css状态层，仅由容器控制排版。浏览器确认5张己方卡均保留card-art与value，无card-name；修订截图已复查。 | Fixed |
| BUG-160 | 2026-10-10 | game-16x9.css / 缩放画布焦点 | 小窗截图中顶部HUD和怪物战场上缘被裁切，内部卡牌边界检查仍通过。 | transform缩放不改变1920×1080排版盒，overflow:hidden父容器仍能因聚焦而滚动；内部game的装饰溢出也形成可滚动区域。只检查子元素位于game内，遗漏两层容器的滚动偏移。 | frame与game两层均使用overflow:clip；五档窗口及卡牌／播放按钮焦点实测确认frame在视口内、game与frame同起点、两层scrollTop均为0，HUD／三排棋盘／操作可见。1280×720修订截图已复查。 | Fixed |
| BUG-161 | 2026-10-10 | game-16x9.html / 整体设计范围 | 只恢复卡面后，HUD、英雄栏、资源条、技能、商品按钮、详情和开始页仍与原版不同，违背只改排版的要求。 | 16:9草案从头重做界面；上次修正只覆盖卡牌组件，未恢复其他组件。 | 整体恢复combat-status.html，只追加布局CSS／脚本；去掉两处引用后与原HTML逐字一致，布局CSS无字体／配色／边框覆盖。新设计产物撤回；五档窗口确认怪物信息在上、英雄在下，三排棋盘完整。原Enter购买、单一详情、三项任务和主题切换检查通过，无浏览器错误。 | Fixed |
| BUG-162 | 2026-10-10 | DisplaySettingsView / 可用分辨率 | 无边框窗口按工作区排除任务栏后，可能错误拒绝与屏幕原生尺寸相同的16:9分辨率。 | 首版统一使用ScreenGetUsableRect，未区分现有无边框窗口与带边框窗口。 | 无边框按ScreenGetPosition／ScreenGetSize检查并居中；带边框仍使用工作区。修正后构建与显示回退回归通过。 | Fixed |
| BUG-163 | 2026-10-10 | 获批HTML迁移 / 等级框与战斗反馈 | 宣布布局执行完成时，等级框扩展与新版战斗反馈仍停留在HTML，未完成获批方案。 | 按16:9子计划收尾，遗漏其引用的战斗反馈计划；功能回归不能验证未接入的视觉项。 | 补齐等级框、发动与状态层、双方持续伤害反馈，242/242及三档原生截图通过；整体字体／材质对齐仍按BUG-127单独验收。 | Fixed |
| BUG-164 | 2026-10-10 | CombatTraceChecks / 周期伤害来源元数据 | 新增伤害来源后旧战斗指纹检查失败，241/242。 | 指纹依赖record.ToString，新只读字段改变序列化文本，原伤害与快照并未改变。 | 旧基线继续比较原字段，不重写基线；新增来源由双方周期伤害专项检查验证，242/242通过，原三组基线一致。 | Fixed |
| BUG-165 | 2026-10-10 | HtmlParityCapture / 导师截图夹具 | 新增导师截图夹具首次构建失败。 | 夹具误从Definition根读取身份字段，并遗漏CardDisplayAdapter命名空间。 | 改为读取Attributes.Identity，补齐命名空间。 | Fixed |
| BUG-166 | 2026-10-10 | LevelPresentation / 容器详情等级框 | 原生截图发现详情等级框挤到正文内侧，与面板边界不重合。 | PanelContainer按内容边距排版普通子节点；独立层还需显式跟随原浮层的全局位置及绘制层级。 | 容器装饰独立于排版，跟随全局边界、层级和淡入；几何／层级断言、三档截图与窗口检查通过，无新增锚点警告。 | Fixed |
| BUG-167 | 2026-10-10 | DisplaySettingsView / 设置弹窗键盘焦点 | 复查发现设置没有独立Tab焦点环，键盘可跳到弹窗后选角控件；重复Open会丢弃预览状态。 | 弹窗只拦截鼠标，未设置焦点关系，Open无重入保护。 | 设置／确认两个阶段均配置正反Tab循环，重复Open保持当前预览；焦点和取消恢复断言通过，242/242回归通过。 | Fixed |
| BUG-168 | 2026-10-10 | HtmlParityCapture / 导师候选等级 | 新导师截图夹具直接复用玩家已获技能，候选等级与导师访问等级不一致。 | 图形夹具绕开导师用例，把库存当作传授候选。 | 改为在隔离夹具对局通过MentorService.Open捕获正式筛选候选，同次访问等级统一且不足三项不补齐；不修改正式内容。 | Fixed |
| BUG-169 | 2026-10-10 | HtmlParityCapture / 战斗数值夹具 | 状态截图里部分卡牌攻击／护甲／中毒显示0，与战前样例不一致。 | 夹具把CardBattleSetup独立数值设为0，覆盖了CombatValues，同时没有传入实例价值、标签和多重。 | 正确传入卡牌冻结数值及价值，重新生成三档状态截图；不改模拟器或正式定义。 | Fixed |
| BUG-170 | 2026-10-10 | CardDetailsView / 原型动效和内边距 | 详情缺少获批HTML的6px位移淡入，正文比HTML宽2px，阴影仍采用旧版本参数。 | 只迁移透明度；等级边框从1px增至2px后仍沿用21px内容边距和旧阴影。 | 分离最终定位与临时位移，恢复6px／120ms淡入；边框加正文留白共22px，采用新版12px偏移阴影。243/243、原生输入及四档截图通过；精确阴影边缘仍属BUG-127。 | Fixed |
| BUG-171 | 2026-10-10 | CardDetailsView / 16:9伸缩 | 1920×1080原生详情宽456px，而获批HTML固定为380px；不同分辨率下字体、徽章和间距随画布缩放。 | 固定在body上的HTML浮层被直接当作画布组件等比放大。 | 详情反向抵消画布伸缩，以屏幕像素排版；定位间距、淡入位移、关闭按钮和等级框同步换算。四档实际窗口及改变／恢复分辨率检查通过。 | Fixed |
| BUG-172 | 2026-10-10 | MatchShell / 商店与遭遇排版 | 原生阶段标题独占左栏，商品／选项占右栏；获批HTML的阶段标题和内容应同在右侧阶段面板，左栏显示英雄。 | 把怪物摘要的左栏位置错误地推广到所有阶段，英雄始终留在左下。 | 普通阶段的原标题组件移入右侧面板左段，英雄回到左上；怪物仍在左上、英雄在其下，保留原组件样式及操作。商品高度按HTML实际窗口vh计算。四档截图、原生卡牌输入与窗口／拖拽检查通过。 | Fixed |
| BUG-173 | 2026-10-10 | MatchShell / 两侧装饰线 | 左侧椭圆装饰压入英雄面板，切换怪物页后高度变化；右侧装饰越界。 | 使用阶段边界和英雄底边定位，原型实际以整个app的左右边缘及上下120px定位。 | 改为画布两侧16px外距、26px宽，上下120px留白；不改线条材质。绘制顺序也恢复到面板之前；四档最终截图复查通过。 | Fixed |
| BUG-174 | 2026-10-10 | CardDetailsView / 已固定详情的分辨率变化 | 改变画布伸缩后详情宽度恢复380px，但相邻16px间距仍沿用原缩放坐标。 | 仅换算浮层尺寸和边界，没有保留卡牌锚点用于重新计算相邻定位。 | 保存只读锚点，伸缩／边界变化时重新计算左右翻转和16px间距；原生截图夹具加入实际窗口变化及恢复断言。四档真实固定详情及分辨率变化／恢复检查通过。 | Fixed |
| BUG-175 | 2026-10-10 | HtmlParityCapture / 固定详情夹具 | 分辨率变化检查误报：宽度及间距正确，详情却被悬停退出清理关闭。 | 夹具直接调用ShowNear，未走右键固定入口，因此页面仍把它视作普通悬停。 | 使用真实卡牌右键输入设置页面固定状态，再改变窗口并核对身份、可见性、宽度和间距。四档捕获及实际缩放检查通过。 | Fixed |
| BUG-176 | 2026-10-10 | HtmlParityCapture / 像素尺寸断言 | 15.999976px间距被判为不等于16px，导致正确缩放检查失败。 | 使用通用浮点相等判断，其容差比真实绘制精度更严格。 | 像素宽度与间距采用0.05px容差，仍拒绝1px以上的可见偏移；身份、可见性和命令隔离断言保留。四档捕获及实际缩放检查通过。 | Fixed |
| BUG-177 | 2026-10-10 | ComponentShowcase / 截图入口 | 截图断言异常后隐藏进程持续运行，无法从退出码判断失败。 | async void入口未捕获异常并终止截图任务。 | 捕获异常、输出完整诊断并以非零退出码结束；成功路径仍由原截图流程退出。 | Fixed |
| BUG-178 | 2026-10-10 | MatchShell / 三排棋盘布局 | 原生三排按剩余高度等分，卡牌被高度预算压小；普通页己方战场比原型低约29px，备战区低约21px。 | 将阶段与两排棋盘强行等高，未按棋盘宽度、10格比例、27px标题和12px区域间距计算自然高度；阶段起点多1px。 | 按原型宽度推导棋盘高度，普通阶段214px、怪物阶段按自身棋盘高度，恢复136px起点及12px间距；243/243边界回归、四档截图、窗口／键盘及原生拖拽通过。 | Fixed |
| BUG-179 | 2026-10-10 | BoardZoneView / 怪物自身战场 | 怪物战场多显示一行“敌方战场”标题，卡牌因此向下偏移27px。 | 直接复用带标题的己方棋盘，未保留原型怪物阶段无标题的包装布局。 | 复用棋盘并仅关闭怪物包装内标题，卡面、槽位、详情与冻结回放不变；10格边界、实例复用与243/243回归、四档截图通过。 | Fixed |
| BUG-180 | 2026-10-10 | 统一HTML / 重建棋盘 | 首次合并后怪物页的己方卡牌没有发动／状态层，浏览器实测层数量0。 | 新样例初始化重绘棋盘后未重新投影既有表现状态。 | 重建棋盘后重新更新只读表现层；最终浏览器状态层5项、窗口及控制台复查通过。 | Fixed |
| BUG-181 | 2026-10-10 | 统一HTML / 拖拽卡面 | 拖拽预览从缩放画布移到body后，图标和文字会按未缩放尺寸绘制。 | 只复制卡牌的屏幕宽高，没有同步画布缩放，导致卡内装饰相对尺寸改变。 | 按逻辑尺寸复制原卡面并同步缩放、保留屏幕抓取偏移；1920／1280实际拖拽、移出取消和禁用检查通过。 | Fixed |
| BUG-182 | 2026-10-10 | 统一HTML / 详情样例 | 属性详情样例出现在窗口左上角，未紧邻原卡牌；数值10仍未着色。 | 以未挂树的克隆作为锚点；属性词语插入span后数值只读取紧邻文本节点，漏过空格。 | 使用不可交互的实际定位锚点，按同段语义为数字赋色；浏览器确认详情紧邻卡牌、攻击与护甲的10分别着色，缩放后仍保持380px宽及约16px间距。 | Fixed |
| BUG-183 | 2026-10-10 | 统一HTML / 分辨率预览 | 设置的应用仅改说明文本与配色，没有可视化不同尺寸的画布预览。 | 初稿未将所选尺寸传入排版预览。 | 以虚拟显示区域呈现所选分辨率和留边，确认保留、取消／Escape／超时恢复；不改变浏览器或操作系统设置；预览／Escape／超时回退检查通过。 | Fixed |
| BUG-184 | 2026-10-10 | CardItemView.DragPreview / 原生出售拖拽 | 用户报告出售拖动时卡面没有跟随鼠标。 | DragPreview只创建卡名Label，没有复用卡面。 | 获批后复用完整CardFace、原缩放与抓取偏移；战场／备战真实鼠标位移、Escape取消及重新出售通过，实际截图卡面可见，保留既有出售命令与金额校验。 | Fixed |
| BUG-185 | 2026-10-10 | 统一HTML / 导师样例 | 初稿把无阵营冲撞误作为2级圣骑士技能，令大主教在2级出现额外候选。 | 复用英雄展示栏示例而未核对正式归属／初始等级；冲撞实际无阵营、1级。 | 大主教1级只捍卫、4级捍卫与至圣斩；三选一使用单独不限阵营排版示例，图鉴标明归属。正式归属、1项／2项／3项示例、空候选与一次领取复查通过。 | Fixed |
| BUG-186 | 2026-10-10 | 统一HTML / 弹窗取消焦点 | 设置Escape关闭后焦点仍停留在隐藏的“保留”按钮。 | 在cancel事件默认关闭之前聚焦弹窗外按钮，被浏览器模态焦点门控拦截；超时回退也会隐藏当前确认按钮。 | 取消事件阻止默认关闭后显式关闭／恢复菜单焦点，回退先聚焦应用；菜单正反Tab、Escape和超时检查通过。 | Fixed |
| BUG-187 | 2026-10-10 | 统一HTML / 空备战详情样例 | 静态复查发现卖完备战卡牌后点击详情样例会空引用，虚拟详情锚点也未跟随画布缩放。 | 样例假定备战始终存在卡牌，隐藏锚点使用一次性的屏幕坐标。 | 允许用空格锚点，定位时同步真实棋盘边界；空备战、长正文及实际1280→1920相邻定位复查通过。 | Fixed |
| BUG-188 | 2026-10-10 | 统一HTML / 开始界面弹窗返回 | 静态复查发现放弃后进入设置或选角再取消，会露出已放弃的旧对局预览。 | 打开子弹窗前提前关闭开始／终局父弹窗。 | 保留父弹窗层，取消子弹窗回到原入口；只有确认英雄时关闭父层开始新预览。放弃／开始／设置／选角的取消及重新确认流程通过。 | Fixed |
| BUG-189 | 2026-10-10 | 统一HTML窗口核对 / 读取时机 | 批量设置浏览器尺寸后立即读取DOM，1280窗口仍记录上一档1920画布；仅判断卡牌在画布内又会误判通过。 | 未等待浏览器新的UI状态，且漏查整个画布位于视口内。 | 每档尺寸后读取最新UI状态再测量，并明确核对画布四边、经济区和菜单均在视口内；四档画布四边、经济区、菜单、上下关系及无整页滚动通过。 | Fixed |
| BUG-190 | 2026-10-10 | DragSaleChecks.Native / 预览抓取断言 | 原生抓取位置检查因约0.014px差值失败。 | 源卡悬浮Tween仍在推进，断言将后续帧的源卡位置与拖拽开始时冻结的预览偏移作浮点完全相等比较。 | 按屏幕观感使用0.5逻辑像素定位容差，保留预览尺寸、20×10鼠标位移及取消／出售的独立断言。 | Fixed |
| BUG-191 | 2026-10-10 | CardEffectRow / CardValueBadge / 统一HTML | 用户指出生命药瓶文字与底板配色难看，先只改字色，又误将所有底板统一。 | 未确认用户要调整的字色／底色关系，先后错误解读需求。 | 用户确认底板使用各属性专属色、数字统一浅色。撤回统一底色，效果底板按对应专属色52%→28%渐变、价值底板为金色35%，数字浅象牙白。构建0警告0错误，HTML计算样式及1280原生两套主题截图通过；旧统一底色截图仅为被否决方案证据。 | Fixed |
| BUG-192 | 2026-10-10 | CardItemView.DragPreview / 原生出售截图 | 实际出售截图中拖动卡面被出售区遮住，坐标跟随检查却通过。 | 预览使用默认ZIndex=0，出售区ZIndex=20；只检查坐标与尺寸没有发现渲染层级遮挡。 | 预览使用独立ZIndex=100置于出售区上方；重新用原生输入与实际截图确认完整卡面可见、抓取跟随和取消／出售通过。 | Fixed |
| BUG-193 | 2026-10-10 | ResourceBar / HTML生命及魔法条 | 用户指出资源条仍为旧绿色／青蓝色底，叠加生命粉色／魔法紫色数字不协调。 | 属性色仅应用于数字和图标，未同步填充资产与HTML底色。 | 生命填充使用生命柔粉红、魔法填充使用淡紫的65%→40%渐变，数值统一浅象牙白；己方及敌方共用资源条同步。构建0警告0错误，1280原生两套主题／敌方摘要截图、窗口变化及恢复检查通过。 | Fixed |
| BUG-194 | 2026-10-10 | 统一HTML / 灼伤示例 | 用户指出灼伤没有体现在HTML中；虽有状态徽章，卡面没有灼伤图标、名称及直达示例。 | effectNames与SVG符号缺少burn，未设置灼伤卡面／详情夹具。 | 补全火焰图标、名称、卡面施加量、详情和0.6s持续伤害示例；仅UI夹具，不新增正式内容；待验证。 | In Progress |
| BUG-195 | 2026-10-10 | HTML / CardKeywordText / 再生术语分类 | 生命再生和魔法再生未明确区分，魔法再生被拆开着色。 | 原生词语匹配遗漏完整再生术语，魔法再生复用魔法色。 | HTML完整术语及对比已验证；项目优先匹配完整词语并关联独立属性key与颜色。两种再生的词语／数值截图、两套桌面主题及246项回归通过。 | Fixed |
| BUG-196 | 2026-10-10 | CardDetailsContent / CardDetailsView | 同内容叠加发现标签比HTML窄3px且矮2px、标题提前2px、正文高度重复计入留白、冷却圆徽右移4px、关闭按钮字号及位置不符。 | 标签未将边框计入内容留白，Godot与CSS字距／字体行盒不同；正文同时设置行距和上下留白；标题尾距与header右留白未精确迁移；关闭沿用12px默认按钮。 | 修正标签边距、标题行盒、正文预算、徽章留白与关闭按钮；四档实际截图、长正文、固定详情缩放、鼠标／键盘输入通过。 | Fixed |
| BUG-197 | 2026-10-10 | CardDetailsView / MatchTheme.DrawSurface | 详情原生阴影呈硬边黑框，淡入进度与HTML不一致。 | StyleBoxFlat软边并非CSS高斯模糊；Cubic Out不同于CSS默认ease曲线。 | 详情改为缓存柔和阴影及CSS ease曲线；中点进度、刷新／关闭重开、四档两套主题截图通过。微小栅格差异按用户最新要求不再追齐。 | Fixed |
| BUG-198 | 2026-10-10 | DisplayLayoutChecks.Settings | 清理output后全量回归245/246，显示设置保存检查失败，headless与原生均可复现。 | 验证夹具写入output/ui-16x9/settings-check.cfg前未创建目录，误将目录缺失视为设置逻辑失败。 | 验证开始时创建自身输出目录并检查返回值；246/246全量验证通过，正式显示设置逻辑未改。 | Fixed |
| BUG-199 | 2026-10-10 | PlaytestVerification.WindowsAndKeyboard | 详情Header新增右留白容器后窗口检查发生Node not found和空引用。 | 本轮组件层级变化后遗漏窗口验证中的两个旧冷却节点路径。 | 同步两处路径；窗口／焦点／键盘、无主动冷却、长正文滚动及边界检查重跑通过。 | Fixed |
| BUG-200 | 2026-10-10 | 详情对照捕获批处理 | 蓝色主题捕获日志仍为森林金，宽屏蓝色文件缺失。 | 批处理将附加参数数组放在原生命令的--之后，未正确展开为用户参数，未校验实际主题。 | 改为显式传入--blue-details并校验实际主题日志；四档两套主题重新捕获通过，宽屏使用独立窗口尺寸后缀。 | Fixed |
| BUG-201 | 2026-10-10 | Godot / 卡牌详情可读性 | 用户反馈项目详情文字过小，HTML原字号没有问题。 | 原生详情沿用12px正文及20px标题，固定屏幕尺寸使大窗口仍显小。 | 项目正文18px、标题24px、标签13px、辅助14px，浮层440px并保留滚动；三档实际详情截图与窗口检查通过，HTML原字号恢复。 | Fixed |
| BUG-202 | 2026-10-10 | Godot / 显示画布基准 | 用户反馈画面缩放后内容大小不协调。 | 当前项目设置缺少1600×900逻辑视口尺寸，偏离既有固定16:9画布约定。 | 恢复1600×900逻辑视口，实际窗口保持等比缩放与宽屏留边；窗口、缩回及键盘检查通过，HTML恢复原缩放。 | Fixed |
| BUG-203 | 2026-10-10 | HTML / PlayerHeroPanel / 英雄小头像 | 战斗小窗口使用整幅原画，脸部太小。 | 普遍使用cover；原生仅对竖图做统一方形裁切，未按主体脸部位置取景。 | 六位英雄按HTML已确认的脸部取景同步为原生AtlasTexture显示区域；原画与选角主图保留完整。六张项目实际截图检查通过，构建0警告0错误、246/246回归通过。 | Fixed |
| BUG-204 | 2026-10-10 | HTML / AttributePalette / 属性色 | 用户反馈恢复类属性等色号重复度过高。 | 生命／治疗／再生过于接近，原生魔法与魔法再生还共用同色，SVG底板和状态徽章残留旧色。 | 十种独立属性色同步项目，魔法再生独立蓝紫、生命再生青色、治疗翡翠绿、生命玫红；图标、详情、效果底板、资源条和状态徽章同步。两套主题实际截图及246/246回归通过。 | Fixed |
| BUG-205 | 2026-10-10 | HTML / 灼伤状态 | 灼伤衰减至0后仍显示“灼伤0”。 | HTML状态渲染未判定剩余量；原生已有amount大于0时显示的规则。 | HTML不大于0隐藏、重新施加恢复，真实衰减及双方隐藏检查通过；原生零灼伤隐藏的既有回归保持通过，无需修改战斗结算。 | Fixed |
| BUG-206 | 2026-10-10 | 统一HTML / 属性数值着色 | 配色截图发现生命再生10与魔法再生5被后文的生命／魔法染色，治疗20也被生命染色。 | 数值先向后寻找属性词，越过紧邻前方的完整属性名称。 | 优先使用紧邻前方的属性标签，后置数值继续保留原匹配；治疗／魔法／两种再生数值断言及恢复详情截图通过。 | Fixed |
| BUG-207 | 2026-10-10 | 本轮需求处理 / HTML字号 | 用户指出HTML文字本来没有问题，却被误改为大字号。 | 把项目文字太小误判为HTML设计问题，并把“不打折扣”当成像素对照。 | 撤回HTML字号、浮层尺寸和缩放修改，四档浏览器核对原12px正文／20px标题／380px浮层；保留头像、属性色和灼伤隐藏，项目修复实际可读性及基本反馈。 | Fixed |
| BUG-208 | 2026-10-10 | MatchTheme.Accent / 插画按钮 / 卡牌焦点 | 主要按钮悬停和按下样式相同，插画挡住默认反馈，键盘卡牌缺少上浮。 | Accent三态共用同一纹理；反馈仅在底层StyleBox；卡牌上浮只监听鼠标。 | 区分三态，上层反馈覆盖动态菜单／操作／遭遇／技能／英雄按钮；卡牌焦点共享上浮发光。实际鼠标渲染差异、移出恢复、Space菜单及购买／领奖／出售键盘检查通过。 | Fixed |
| BUG-209 | 2026-10-10 | CardItemView / SelectionOutline布局 | 原生验证出现大量锚点尺寸覆盖警告。 | FullRect锚点轮廓又显式设置Size，跟随卡面时发生冲突。 | 改用TopLeft锚点，LayoutFace统一定位到实际卡面；回归及原生输入重跑通过，锚点覆盖警告消失。 | Fixed |
| BUG-210 | 2026-10-10 | ButtonFeedback / Godot枚举 | 首次构建反馈层失败。 | 误用DrawModeEnum，当前SDK类型名为BaseButton.DrawMode。 | 查阅本地SDK并修正枚举名，构建0警告0错误。 | Fixed |
| BUG-211 | 2026-10-10 | 原生反馈截图检查 / 等待时机 | 遭遇按钮移出后的恢复检查失败。 | 仅等待8帧，不能保证120ms淡出结束。 | 等待180ms实际时间再取帧，正常／悬停／按下图像有差异，移出释放后恢复到正常；三类真实按钮检查通过。 | Fixed |
| BUG-212 | 2026-10-10 | CardDetailsContent / 字号放大后的详情刷新 | 全量回归245/246，展开详情在同页刷新时向上跳。 | 换行标题保留窄宽度下的旧高度，容器延迟排版；展开区也延迟扣减正文预算，刷新将暂时超高的详情强制上移。 | 给标签／标题明确可用宽度，立即排序标签并计算标题行数；展开／Render立即重算共用高度。246/246、原生三档详情及窗口检查通过，位置与展开状态保持。 | Fixed |
| BUG-213 | 2026-10-10 | CardDetailsContent / 容器通知 | 首次加入即时排版通知后构建失败。 | SDK通知常量为long，Notification参数要求int。 | 显式转换通知常量，构建0警告0错误并通过246项回归。 | Fixed |
| BUG-214 | 2026-10-10 | UI_SYSTEM.md / 配色规范 | 同一文档同时保留现行属性色表与旧关键词色表，数值着色说明也冲突。 | 属性色迁移后遗漏旧表同步。 | 现行色值统一维护于属性配色表，删除旧冲突表，说明完整再生术语及关联数值规则。 | Fixed |
| BUG-215 | 2026-10-10 | HtmlParityCapture / 两主题配色样例 | 初次蓝色主题截图恢复成原卡说明，无法比较同一内容。 | 样例只传给详情面板，主题刷新从冻结页面快照取回同ID原卡。 | 样例同步到只读页面快照，重新捕获同一十种属性说明的两主题截图并检查。 | Fixed |
| BUG-216 | 2026-10-10 | 本轮文档更新脚本 | 初次文档更新脚本未执行。 | 临时脚本中出现无效索引赋值语法，解析阶段中止。 | 删除无效语句后重新执行；首次失败未写入文件，文档及差异检查完成。 | Fixed |
| BUG-217 | 2026-10-10 | MatchTheme / 技能与图鉴详情字号 | 卡牌详情放大后，技能、图鉴与其他说明仍明显偏小。 | RichTextLabel没有独立字号，继承14px默认字体；普通悬停提示仍12px。 | 统一富文本四种字重为18px、普通提示14px，保持已有局部字号及滚动；两档原生技能／卡牌图鉴截图、长说明滚到底及246/246回归通过。 | Fixed |
| BUG-218 | 2026-10-10 | SkillItemView / 技能列表选中状态 | 技能列表出现突兀白底，选中和未选中难以区分。 | SetSelected两种状态都使用同一白色填充，等级框又遮弱边缘差异。 | 未选中恢复深色底，选中使用主题高亮底并保留等级边框；技能图鉴实际悬停／按下／松开恢复、两档页面及246/246回归通过。 | Fixed |
| BUG-219 | 2026-10-10 | HeroDetailsView / 技能、套装、奖励浮层键盘焦点 | 打开浮层后焦点留在背景按钮，Tab也可能离开浮层。 | ShowSection未转移焦点，关闭未还原入口，没有浮层内Tab边界。 | 打开聚焦关闭按钮，焦点位于浮层时Tab保持在浮层内；关闭恢复仍有效的原入口，不抢其他弹窗焦点。原生Tab／Enter关闭返回及246/246回归通过。 | Fixed |
| BUG-220 | 2026-10-10 | MatchPresenter / ButtonFeedback / 详情Escape关闭 | Escape关闭商品详情后偶发重新打开。 | 原生1920窗口重复检查第10次复现：无选中项也执行取消刷新，商品节点重建；反馈层写光标触发EnterHover重新打开详情，调用栈来自ButtonFeedback._Process。 | 无选中项不重绘；反馈层仅在光标类型变化时更新。三档各12次关闭／重新查看及商品节点保持、购买／领取／出售、真实拖拽取消与出售、246/246回归通过；构建0警告0错误。日志output/ui-html-parity/escape-*.log。 | Fixed |
| BUG-221 | 2026-10-10 | CardInteractionChecks / 临时关闭诊断构建 | 增加重新打开调用栈诊断后构建失败。 | Environment同时来自Godot与System，名称有歧义。 | 明确使用System.Environment；重建0警告0错误通过，诊断成功捕获实际重新打开调用栈。 | Fixed |
| BUG-222 | 2026-10-10 | MatchTheme / 图鉴 / 选角配色 | 星辉蓝配色下普通按钮、搜索框与部分选中底色仍为森林绿。 | 通用控件使用固定绿色，图鉴搜索框又覆盖固定绿色；技能已选条目切换主题时未重新投影。 | 按现行蓝色桌面色值同步控件状态，图鉴搜索框使用共享主题，技能选中样式刷新但保留筛选、等级和焦点；两档图鉴切换及恢复、两主题按钮真实反馈和246/246回归通过。 | Fixed |
| BUG-223 | 2026-10-10 | MentorChecks.Capture / 截图缩放基准 | 导师1280截图中备战区被窗口底部裁切，未呈现正式试玩的等比画布。 | 旧截图夹具把逻辑视口改为实际窗口尺寸，偏离1600×900基准。 | 捕获保持1600×900逻辑视口，只调整实际窗口；1280／1920捕获及完整备战区边界检查通过，1280截图已复查。 | Fixed |
| BUG-224 | 2026-10-10 | CardInteractionChecks / 原生出售回归 | 两主题反馈检查通过后，Enter出售确认断言失败。 | 原日志未区分金币、库存或详情重新打开，尚未确认根因。 | 增加详情身份、实际／期望金币、原卡是否存在及焦点诊断；随后原生回归通过，未作为修复证据。用户要求停止扩展细节检查，保留记录待复现。 | Open |
| BUG-225 | 2026-10-11 | DefinitionChecks.CheckUnknownTagDisplay | 登记龙族标签后，未知标签兜底验证错误地失败。 | 旧夹具使用Dragon作为未知标签，与新正式标签冲突。 | 改用verification.unknown_tag专用未知key，继续检查原始key兜底；辉耀巨龙验证独立检查龙族中文映射。 | Fixed |
| BUG-226 | 2026-10-11 | AbilityDefinition / SummonAdjacentCardEffectDefinition | 骨冢配置3秒主动召唤后，定义注册抛出异常，正式内容池无法加载。 | 相邻召唤校验只接受PassiveOnBattleStart，未支持请求的Active触发。 | 固定模板相邻召唤允许Active或PassiveOnBattleStart，保留自身目标与战场限制；新增验证覆盖3秒首次召唤、同级属性、骷髅完整冷却、重复发动阻挡、边界、备战与敌方隔离及确定性。构建0警告0错误，251/251回归通过，现有开战召唤与固定战斗基线通过。 | Fixed |
| BUG-227 | 2026-10-11 | ArmyPriestCardDefinition / army_priest-illustration.png | 随军牧师为小型卡牌，但旧原画1254×1254，和1:2卡面不匹配，填满时会裁掉左右内容。 | 旧方形原画仍被小型Definition引用，历史比例调整遗漏此卡。 | 通过图像工具重绘为887×1774（1:2）并更新正式插画引用，旧图保留；主体、实际58张卡面截图及纹理加载复查通过，构建0警告0错误、251/251全量回归通过。 | Fixed |
| BUG-228 | 2026-10-11 | audio/voices/heroes/paladin-selection.mp3 / 圣骑士选角配音 | 用户反馈帕拉帝恩声音过于稚嫩，希望更成熟；首次调整后反馈低沉过度，希望更有气魄。 | 原配音采用偏活泼的Yunxi音色；首次Yunjian版本的语速-10%、音调-20Hz调整过重。 | 保留Yunjian男声，调整为语速-2%、音调-5Hz，台词不变；Godot重新导入、2.66秒音频解码非静音及251/251回归通过，声音效果待用户试听确认。 | Fixed |
| BUG-229 | 2026-10-11 | CardDisplayAdapter.FaceEffects | 辉耀巨龙详情包含百分比灼伤，但卡面只显示攻击，漏掉3级10／4级20灼伤。 | 卡面效果映射未处理ApplySourceAttributePercentStatusEffectDefinition。 | 萤火吉丁虫并行改动已补充通用灼伤计算及百分比显示；本次增加3／4级、攻击加成取整和灼伤施加量加成的卡面回归验证，构建0警告0错误、254/254全量回归通过。 | Fixed |
| BUG-230 | 2026-10-11 | FireglowJewelBeetleChecks.ClassificationAndPercent | 新验证夹具首次构建失败，无法检查狂暴与多重灼伤。 | 将只读普通类BattleSetup、BattleSideSetup误作record使用with表达式。 | 在内部卡牌Definition配置开战狂暴，通过实例属性配置多重，沿用工厂生成冻结输入；构建0警告0错误，三种灼伤与狂暴多重验证通过，254/254全量回归通过。 | Fixed |
| BUG-231 | 2026-10-11 | AdventurerHuntingBowChecks.RewardsAndPlacement | 新奖励验证首次构建失败。 | 将整体棋盘的Contains查询误用于BoardZoneState，区域模型仅提供Placements。 | 使用战场Placements按CardId查询放置；构建0警告0错误，兽皮放置、同级奖励、合并及满盘保留验证通过，258/258全量回归通过。 | Fixed |
| BUG-232 | 2026-10-11 | AdventurerBackpackChecks.HeroGrowthAndSet | 新套装组合验证首次运行失败。 | 夹具在已结算首轮期待补发套装经验，初始1经验加8经验未达到10经验升级门槛。 | 推进到下一轮，验证正式2件套新增1经验，再补8经验达到升级门槛；保留旧战斗输入与新等级加成对比。构建0警告0错误，261/261全量回归通过。 | Fixed |
| BUG-233 | 2026-10-11 | RuinStoneStatueCardDefinition / 开战描述词条 | 新定义初稿引用不存在的开战词条常量，源码核对发现无法编译。 | 将能力触发名称BattleStart误用为描述词条常量，正式描述词条为Opening。 | 改为既有CardKeywords.Opening，保留PassiveOnBattleStart触发；构建0警告0错误，263/263全量回归通过。 | Fixed |
| BUG-234 | 2026-10-11 | 全量验证 / 并行新增碎骨法师原画 | 石像护甲验证通过，但正式原画投影检查失败，首次全量262/263。 | 同时新增的bonebreaker_mage-illustration.png尚无Godot导入资源，加载退回占位图。 | 使用Godot编辑器正常导入新增资源，确认导入文件已生成，再重跑全量263/263通过；未修改该卡牌定义或图片。 | Fixed |
| BUG-235 | 2026-10-11 | AbilityDefinitions / 摧毁伤害效果校验 | 新增效果校验初稿重复插入条件行，源码核对发现语法不完整。 | 补丁将旧条件行误作新增行。 | 移除重复行，并加入尺寸、元素、伤害属性、主动触发及敌方英雄目标校验；构建0警告0错误通过。 | Fixed |
| BUG-236 | 2026-10-11 | BonebreakerMageChecks.RepeatedSummoningAndPlayback | 首次全量264/265，重复召唤验证未通过。 | 验证对手使用正式莫娜的200生命，被骷髅与法师伤害提前击败，未能覆盖第18秒重复召唤。 | 将验证对手生命设为10000，保持正式卡牌能力与召唤规则；原卡与召唤物位置复用、回放无重叠及跨战斗隔离验证通过，265/265全量回归通过。 | Fixed |
| BUG-237 | 2026-10-11 | MischievousBatChecks / 卡面快照验证 | 新卡验证首次构建失败，ShopOffer未解析。 | 验证文件遗漏ShopOffer所在Domain.Match命名空间。 | 补充命名空间引用，构建0警告0错误；保留真实商品快照与卡面投影验证。 | Fixed |
| BUG-238 | 2026-10-11 | GemShopService / 棋盘归属预检 | 宝石商店初稿构建失败，无法查询整体棋盘Placements。 | 整体BoardState仅提供Contains与分区Placements。 | 使用BoardState.Contains查询己方棋盘归属；构建0警告0错误通过。 | Fixed |
| BUG-239 | 2026-10-11 | GemShopChecks / 新验证夹具 | 首次全量268/270，主值多重及砧板场景两项失败。 | 石像开战禁锢随多重重复，夹具仍期待8秒首次发动；隔离注册表遗漏背包引用的冒险者套装。 | 将多重石像观察窗口设到第10秒并核对开战及主动共4次发动，隔离注册表包含所需套装；原输入和升级保留、状态与真实砧板验证通过，270/270回归通过。 | Fixed |
| BUG-240 | 2026-10-11 | GemShopView / GemAnvilDropZone | 初次1280截图中砧板向下撑到棋盘，说明和价格不可见。 | 自动换行Label的初始窄宽度及纵向容器最小高度共同撑高中央区域至408，超过宿主214。 | 使用固定宿主内布局，左侧绘制宝石砧板、右侧放置说明和价格，保留底部拖放提示；补充商店与砧板边界校验，1280及1920原生截图通过。 | Fixed |
| BUG-241 | 2026-10-11 | MatchShell / 宝石商店拖放命中 | 用户反馈镶嵌与出售冲突：实际拖动卡牌时砧板被出售遮罩覆盖，无法正常镶嵌。 | SellDropZone在拖拽开始时显示于整个上方阶段区，ZIndex高于砧板；原验证直接调用砧板虚方法，未覆盖原生GUI命中。 | 宝石商店的出售遮罩改放左侧英雄区域，其他页面沿用上方出售区；新增原生鼠标验证双棋盘镶嵌预览／取消／提交与独立出售通过，截图确认拖拽出售层不覆盖砧板。 | Fixed |
| BUG-242 | 2026-10-11 | GemShopChecks.NativeDrag / 出售输入夹具 | 新原生验证首次在最后的独立出售断言失败，镶嵌已通过。 | 夹具用一次大幅移动同时启动拖动并进入刚显示的遮罩，未补齐区域出现后的鼠标命中更新。 | 参照已有DragSaleChecks，进入区域后补一次鼠标移动再松开；原生镶嵌、取消与出售均通过。 | Fixed |
| BUG-243 | 2026-10-11 | MechanismHeavyCrossbowChecks / 动态标签夹具 | 首次全量274/275，相邻冷却验证失败。 | 将被动标签光环误配置为主动，期待其在5秒直接改变标签；该效果按持续光环求值，不执行标签写入。 | 使用通用自身发动任务在5秒解锁机械标签；另用真正被动标签光环验证有效标签参与相邻筛选。目标卡牌两项验证通过。 | Fixed |
| BUG-244 | 2026-10-11 | ComponentShowcase.CaptureCardPool / 截图夹具 | 命令指定1920×3900，实际截图仍为1920×1080，未覆盖位于后部的新卡牌。 | 捕获工具固定ContentScaleSize为1920×1080，输出来自逻辑视口。 | 保留现有全卡池工具，新增目标卡牌独立SubViewport捕获，恢复被覆盖的旧截图；400×320实际卡面已复查，主体和完整弩形可辨。 | Fixed |
| BUG-245 | 2026-10-11 | MechanismHeavyCrossbowChecks.Capture / 构建 | 目标卡牌截图入口首次构建失败，CardItemView类型未解析。 | 缺少Presentation.Playtest命名空间引用。 | 补充实际组件所在命名空间；构建0警告0错误，卡面捕获与两项验证通过。 | Fixed |
| BUG-246 | 2026-10-11 | MatchDisplayQuery / CardDisplayAdapter / 卡面最终值 | 用户反馈卡面攻防等属性未完整展示当前最终值，多重不显示。 | 备战读取实例基础战斗属性，缺少光环求值；部分公式只显示文字，战斗施加量与卡面未共用冻结数值，多重仅在详情显示。 | 战斗层提供只读光环预览及效果施加量快照，备战和回放投影读取冻结值，增加独立多重标记；聚焦验证、三种尺寸实际卡面及279/279回归通过。 | Fixed |
| BUG-247 | 2026-10-11 | CardFinalValueChecks / 验证初稿 | 源码检查发现CardBattleSetup构造参数错误。 | 将只读CombatValues初始化属性误写成构造参数。 | 改用对象初始化属性；构建0警告0错误，279/279回归通过。 | Fixed |
| BUG-248 | 2026-10-11 | CardFinalValueChecks.FaceScene / 构建 | 首次构建失败，无法访问CardFace.Render。 | 将私有渲染方法误用为组件公开入口。 | 改用既有SetCard入口；构建0警告0错误，三种尺寸组件验证及卡面捕获通过。 | Fixed |
| BUG-249 | 2026-10-11 | CardEconomyService / 合成价值 | 用户要求合成后补偿高等级价值，当前升级仍保留低等级现值。 | 合成仅刷新基础战斗配置，没有处理等级标准获得价值差额；原规则约定完全保留现值。 | 按用户确认的新规则补标准获得价值差额，保留成长及Modifier，连续合并只给最终保留卡补差；所有获得与购买入口先校验溢出。三级尺寸、获得增值、连续合并、出售及失败无副作用验证和279/279回归通过。 | Fixed |
| BUG-250 | 2026-10-11 | CardFinalValueChecks.Preparation / 套装夹具 | 聚焦验证无法预览冒险者背包。 | 夹具套装目录为空，未提供正式背包引用的冒险者套装。 | 夹具补齐正式AdventurerSetDefinition；背包数值预览、聚焦验证及279/279回归通过。 | Fixed |
| BUG-251 | 2026-10-11 | CardFinalValueChecks.Playback / 生命比例夹具 | 聚焦生命比例施加量验证失败。 | 夹具选取超时后的寂灭快照，双方生命归零，当前生命比例伤害预览自然为0。 | 选择最后一份英雄仍存活的发动快照，当时60伤害与15灼伤均与真实事件一致；聚焦验证及279/279回归通过。 | Fixed |
| BUG-252 | 2026-10-11 | 经济与价值成长回归夹具 | 首次全量275/279，获得折半、碧玉蟾成长与登神者升级三项使用旧价值规则而失败。 | 获得来源夹具复用同一对局触发合成，另两项仍期待升级现值不变；与用户确认的补等级差额规则冲突。 | 获得来源改用独立对局检查首次折半；碧玉蟾补中型1→2级的2点价值，登神者校验保留成长并补对应等级差额；固定战斗基线不修改，279/279回归通过。 | Fixed |
| BUG-253 | 2026-10-11 | BattleSetupFactory / 备战区回放数值 | 源码核对发现战斗回放将备战卡攻击显示为0，其他成长及宝石参数也未完整冻结。 | 备战输入使用占位攻击0／冷却1及未组合能力，没有复制完整CombatValues。 | 冻结备战卡自身真实数值与组合能力，仍由IsOnBench禁止发动及战场光环；备战卡面值与无发动验证、279/279回归通过。 | Fixed |
| BUG-254 | 2026-10-11 | BattleEffectResolver / 共享工作区构建 | 短刃斥候首次构建遇到AttributeDamageAmount未定义。 | 共享伤害文件当时正在写入，调用先于方法到位。 | 重新核对后该通用方法已完整写入，不覆盖并行修改；重建0警告0错误。 | Fixed |
| BUG-255 | 2026-10-11 | AssassinDaggerChecks / 新验证夹具 | 刺客匕首首次构建出现两处编译错误。 | Tick.Value为long却与int数组比较；伤害事件来源字段误写为SourceId，实际为SourceCardId。 | 时间期望改为long数组，来源筛选使用SourceCardId；构建0警告0错误，刺客匕首三项检查及285/285回归通过。 | Fixed |
| BUG-256 | 2026-10-11 | 全卡池插画验证 / 共享工作区导入 | 短刃斥候首次全量281/282，唯一失败为全卡池插画，短刃斥候三项通过。 | 同时新增的刺客匕首定义已参与构建，其原画导入晚于首次验证启动。 | 原画由Godot完成导入后重新构建，全卡池插画及285/285全量通过；未改变原验证预期。 | Fixed |
| BUG-257 | 2026-10-11 | CONTENT_DATA / 战斗开始的相邻召唤 | 修复位置规则核对时发现该段仍声称已摧毁卡牌占格，与现行召唤及GAME_DESIGN不一致。 | 此前召唤位置复用已实现，但开战召唤段落残留旧约定。 | 同步为仅存活卡牌阻挡，明确已摧毁位置可复用；原有重复召唤与新增占位修复回放验证、289/289全量回归通过。 | Fixed |
| BUG-258 | 2026-10-11 | CombatTraceChecks / 英雄快照字段兼容 | 连枷骑士全量289/291，固定战斗指纹失败，目标卡两项通过。 | 并行新增隐身字段使HeroBattleSnapshot.ToString追加零值字段，旧数值与事件未变但文本哈希改变。 | 旧场景显式断言双方隐身为0，序列化时仅去掉新增零字段，保留原字段与三个基线哈希；固定战斗及291/291回归通过。 | Fixed |
| BUG-259 | 2026-10-11 | 全卡池插画验证 / 并行原画导入 | 同次全量的另一失败为全卡池插画。 | 暗影斗篷原画已复制，但Godot导入在验证启动时尚未完成。 | 通过Godot编辑器统一导入当前资源后再构建，全卡池插画及291/291回归通过；原验证预期不变。 | Fixed |
| BUG-260 | 2026-10-11 | ShadowCloakChecks / 新验证夹具 | 暗影斗篷验证首次构建找不到ShopOffer。 | 遗漏其所在的Domain.Match命名空间。 | 添加对应引用，构建0警告0错误，四项聚焦检查及295/295全量回归通过。 | Fixed |
| BUG-261 | 2026-10-11 | ConquerorFortressChecks.TriggerBoundaries | 首次全量297/298，敌方发动边界验证失败。 | 验证将技能夹具配置为卡牌式主动冷却，现行模拟器仅为卡牌推进主动冷却，故技能并未发动。 | 改用真实技能支持的开战触发，确认技能成功执行但不触发堡垒；其余卡牌边界检查保留，三项堡垒检查及301/301全量通过。 | Fixed |
| BUG-262 | 2026-10-11 | 全卡池插画验证 / 并行原画导入 | 堡垒第二次全量300/301，唯一失败为全卡池插画，三项堡垒检查通过。 | 同时新增的精钢战斧定义已参与构建，原画导入晚于该次验证完成。 | 统一完成Godot资源导入后重跑，全卡池插画及301/301全量通过；保持原检查预期。 | Fixed |
| BUG-263 | 2026-10-12 | game-16x9.html / 卡牌详情初始化 | 详情改为按当前数值生成文案后，旧初始化仍按数组下标修改碧玉蟾描述，可能中断原型初始化；浆果描述被旧固定值覆盖。 | 旧文案补丁假定desc始终为数组，未适配动态文案。 | 移除已由统一短文案覆盖的两处旧补丁，保留元素设置；浏览器启动及等级切换验证。 | Fixed |
| BUG-264 | 2026-10-12 | game-16x9.html / 等级预览价值 | 新增预览将机关重弩2→4级价值显示为15，标准等级差额应显示24。 | 漏掉默认价值系数2，在获得折半后重复除2。 | 按当前实例价值加尺寸×等级倍率差额，保留已有增值；核对默认系数与CardValueCalculator，浏览器等级切换及预览无副作用验证。 | Fixed |
| BUG-265 | 2026-10-12 | CardDisplayAdapter.Rules / 原生同步构建 | 新描述格式化首次构建出现四处局部变量重名。 | switch模式变量status与同一能力范围的触发状态变量同名。 | 触发状态变量改名triggerStatus，保留效果格式化逻辑；重新构建验证。 | Fixed |
| BUG-266 | 2026-10-12 | UiPrototypeSyncChecks.Scene / 检查初稿 | 源码检查发现预览控件检查将Panel读取为HBoxContainer，运行会类型转换失败。 | Panel实际为PanelContainer。 | 使用Control获取Panel后查找等级按钮，检查范围保持不变。 | Fixed |
| BUG-267 | 2026-10-12 | MatchTheme.Icon / 多重图标 | 原生启动找不到multicast图标资源。 | 新资源命名为icon-multicast.svg，而现有图标加载约定为html/{key}.svg。 | 资源改为multicast.svg并由Godot重新导入，保留统一加载入口。 | Fixed |
| BUG-268 | 2026-10-12 | CardInteractionChecks.DirectAcquisition / 原生同步回归 | 全量303/304，唯一失败为右键交易门控检查。 | 检查仍断言旧固定详情；新预览入口还会隐藏来源奖励浮层，关闭后不恢复。 | 改查放大预览并在交易前关闭；预览保留来源奖励浮层，继续校验旧按钮不交易、只读及重复领取门控。 | Fixed |
| BUG-269 | 2026-10-12 | CardDisplayAdapter / 套装与消耗文案 | 原生截图中套装最大生命显示英文MaxHealth，药水消耗并入发动正文，背包卡面仍显示等级公式。 | 属性名映射缺少生命／魔法组，消耗识别错误地依赖目标为SelfCard，光环卡面未读取冻结英雄等级。 | 补齐资源名称，按通用自身摧毁效果单列消耗；卡面光环显示当前数值，保持正式定义与战斗逻辑不变。 | Fixed |
| BUG-270 | 2026-10-12 | CardFace / 套装名称 | 原生背包卡面左上套装名称被截为“冒险”。 | 名称宽度预算与样式默认左右留白不一致。 | 明确左右各4px留白，与测量宽度一致；仍限制在元素徽章之前。 | Fixed |
| BUG-271 | 2026-10-12 | CardDisplayAdapter.Rules / 标签与元素触发描述 | 随军牧师截图中两项触发条件分别列出，容易误读为同时要求人类与光属性。 | 格式化器未表达EchoOnMatchingAlliedCardActivated的标签／元素任一匹配规则。 | 仅该触发类型同时配置两项时以“或”连接，复用通用描述；不改变战斗筛选。 | Fixed |
| BUG-272 | 2026-10-12 | CardDisplayAdapter.FaceEffects / 魔能盾卡面 | 用户截图中同一卡面显示“累计魔法”和“0”两个护甲徽标，容易误读为重复效果。 | 魔能盾以累计魔法护甲和卡牌护甲加成两个通用效果组合；卡面逐效果生成徽标，将为0的加成也单独显示。 | HTML与原生统一合为一个护甲徽标：零加成显示累计魔法，非零显示累计魔法+数值，回放显示冻结施加量之和；保持正式能力和战斗计算。零／非零／冻结值与后续效果索引验证、全量验证及原生两档加成截图通过，构建0警告0错误。 | Fixed |
| BUG-273 | 2026-10-12 | AscendantChecks.MergePreservesProgress | 登神者新增任务解锁的开战飞行后，合并回归误报永久攻击成长不符。 | 原验证以非回响AbilityActivatedEvent计数，包含Tick 0开战能力，错误作为主动发动成长次数。 | 按登神者每次主动发动的一次直接伤害统计，排除开战能力；构建0警告0错误、313/313全量回归通过。 | Fixed |
| BUG-274 | 2026-10-12 | project-star-card-artwork/SKILL.md / 原画参考候选 | 原画技能指向已弃用的随军牧师、盾甲士和药水版本，生成时容易误用旧画风。 | 参考候选硬编码早期文件名，没有读取当前Definition引用。 | 改为从当前正式Definition的Illustration选择参考；按用户要求删除弃用原画，同步旧图集、历史链接与过期提示词，保留当前资源。 | Fixed |
| BUG-275 | 2026-10-12 | `scripts/Presentation/Playtest/PlaytestVerification.cs` / `FormalIllustrations` | 图鉴排序回归中，原画传递与缺图占位检查失败；全量315/316，图鉴排序交互通过。证据：`output/catalog-sort-verify-2026-10-12.log`。 | 待定位具体卡牌及失败条件；当前工作区包含原画删除与新增，尚不能确认原因。 | 后续定位失败条目，核对当前Definition资源与Godot导入状态后修复；本次排序改动不扩展到原画维护。 | Open |
| BUG-276 | 2026-10-12 | AdventurerHuntingBowChecks / AdventurerBackpackChecks | 新增第二个正式套装后，冒险者验证对套装快照调用无条件Single抛异常。 | 验证错误假定全目录只有一个套装；快照原本就包含全部正式套装。 | 按set.adventurer key选择目标，保留阈值、冷却及旧输入断言。 | Fixed |

| BUG-277 | 2026-10-12 | MatchShell.ShowHoverCard / 图鉴 | 图鉴打开时卡牌悬停被屏蔽；移除固定详情栏后无法查看提示。 | 悬停门控排除了整个卡牌图鉴，详情绘制层也低于图鉴。 | 放开图鉴悬停并提高详情层级，图鉴预览使用无对局上下文；复用局内右键详情，Escape优先关闭浮层。 | Fixed |
| BUG-278 | 2026-10-12 | CardDetailsContent.Render / HTML detail-set | 用户对比截图发现原生卡牌详情的套装区域与HTML原型不一致：原生为连续段落，HTML为独立分区、件数／效果／状态同行。 | 原生仅以AddParagraph输出套装标题和阈值，未同步HTML detail-set的分隔线、标题层级及三列结构；HTML冒险者5件说明另固定为+40，正式定义按英雄等级×10计算。 | 按当前HTML同步套装独立分区、三列阈值、名称标签和出售文案；HTML固定值改等级公式，原生继续读取正式能力及冻结英雄等级。两档原生截图检查与328/328回归通过，构建0警告0错误；HTML仅源码语法检查，浏览器本地文件预览被策略拦截。 | Fixed |
| BUG-279 | 2026-10-12 | HtmlParityCapture / 套装截图夹具 | 新增夹具首次构建失败，误用RequiredCount读取正式阈值。 | 混淆Definition.RequiredDistinctCards和Snapshot.RequiredCount。 | 夹具从RequiredDistinctCards构建快照；构建0警告0错误，两档原生截图及三列状态／英雄等级数值检查通过。 | Fixed |
| BUG-280 | 2026-10-12 | PlaytestVerification / 出售确认文案 | 套装同步首次全量327/328，真实卡面交易检查失败。 | 检查仍期待旧“回补”按钮文案，与同步后的“获得 N 金币”不一致。 | 仅更新文案断言，保留真实出售与奖励交易断言，重新运行全量回归。 | Fixed |
| BUG-281 | 2026-10-12 | CardPreviewQuery.Side / 等级与宝石预览 | 用户截图中猛犸近卫4级护甲显示0而非360；同入口影响臂铠、斗笠、秩序板甲、遗迹石像、盾甲士、荆棘甲及魔能盾护甲加成。斗笠40+7预览实际0可复现。 | 预览组装CardBattleSetup只传CombatValues，没有传独立ArmorAmount；BattleCardState以ArmorAmount初始化护甲，默认0覆盖预期值。实际战斗BattleSetupFactory已正确传入护甲。原验证只覆盖初始卡面和战斗，未覆盖护甲等级切换。 | 共享入口从OwnValues传入ArmorAmount，保持只读；全卡池中护甲能力卡逐级交叉验证等级／宝石预览、+7成长及自身光环。329/329回归通过，构建0警告0错误；猛犸近卫3→4级原生截图护甲和文案均360。证据output/preview-armor/。 | Fixed |
| BUG-282 | 2026-10-12 | scripts/Presentation/Playtest/MatchTheme.cs / CreateBackground | 用户反馈原生全屏背景缺少HTML的渐变纹理。HTML使用两层径向渐变与115°重复细纹；原生转为SVG radialGradient和pattern，视觉未保持一致。 | 待实际原生渲染验证；疑似SVG pattern／渐变转换兼容性，不能仅凭实现存在认定已同步。 | 对照当前HTML检查生成纹理与实际背景，验证SVG支持及渐变范围，按结果修正表现层。 | Open |
| BUG-283 | 2026-10-12 | CardDisplayAdapter.Rules / 卡牌详情交易词条 | 炼金釜等响应出售其他卡牌的永久成长在详情显示为“出售”。 | SaleAttributeBonuses生成文案硬编码了出售标签，未使用已有交易分类；关键词高亮遗漏交易。 | 将其他卡牌出售成长标为交易，补齐高亮与购买／出售其他卡牌的术语边界；全卡池逐级检查交易与自身出售分类。 | Fixed |

| BUG-284 | 2026-10-12 | CardKeywordText / AttributePalette / HTML 描述配色 | 卡牌描述中的修复、摧毁、转变、召唤未高亮，飞行、狂暴缺少专属色；隐身未识别。 | 动作词未加入关键词匹配，飞行和狂暴未映射语义色而回退通用青色；HTML 也缺少对应描述匹配。 | 补齐动作词的青色高亮，飞行浅蓝、狂暴橙红、隐身紫色同步 HTML 与原生描述渲染；不改变内容定义与战斗机制。 | Fixed |

| BUG-285 | 2026-10-12 | HtmlParityCapture.Run / 固定详情缩放截图 | 通用截图在窗口缩放步骤提前退出：详情宽660、邻接间距24、Visible=false，未继续捕获状态页。 | 待定位；截图夹具的1280→1920窗口切换结果未满足440px屏幕宽度与16px间距约定，与此次纯关键词着色无关。 | 后续专项复查窗口伸缩与固定详情可见性；本次保留截图失败日志output/description-colors-capture.log，不改动布局。 | Open |
| BUG-284 | 2026-10-12 | MatchDisplayQuery / CardPreviewQuery / CardDetailsContent | 用户截图中弗兰肯特发动卡牌不显示CD；战场和等级／宝石预览均可能漏显。 | 战斗预览的Values不包含冷却属性，投影直接替换CurrentValues后，详情读取默认0并隐藏圆徽；计算后的CooldownDurationUnits未传给卡牌显示快照。 | 新增只读CurrentCooldownSeconds传递战斗层冻结时长，详情、预览和当前属性共用；回放读取同一冻结时长，100%缩减显示0秒，被动卡隐藏。 | Fixed |
| BUG-285 | 2026-10-12 | CardItemView.DragPreview / MatchShell.HideHoverCard | 选中卡牌后反复拖放，拖拽开始但松手不能移动；用户反馈棋盘拖不动。 | 拖拽只隐藏悬停详情，固定详情保留在棋盘上并拦截目标区域的原生GUI输入。 | 拖拽开始时关闭固定／悬停详情并解除固定标记；真实页面补测小中大型卡牌、选中后拖放、空格／占用格推挤、跨区、重复移动、出售及Escape取消。修复前真实页面复现失败，修复后原生输入通过。 | Fixed |
| BUG-286 | 2026-10-12 | CardDisplayAdapter.Rules / 天启4件套详情 | 日蚀伤害倍率省略敌方英雄目标，易误读为双方受伤翻倍。 | 原生生成器统一省略EnemyHero目标；日蚀受伤倍率效果需要明确受影响方。正式定义、战斗及HTML已仅指向敌方。 | 对日蚀受伤倍率保留目标文案，增加天启全卡各等级套装说明断言；保留双方视角的己方2／敌方4日蚀伤害回归。 | Fixed |
| BUG-287 | 2026-10-12 | 全量验证 / 插画与冒险者日志 | 日蚀目标文案修复后的全量验证330/333，插画传递、冒险者日志身份成长及套装计数三项失败。 | 新插画导入尚未齐备；日志夹具漏传套装注册表、以不支持的1级创建冒险者大剑，并误按三张合并编排。早期夹具还误用BattleResult字段。 | 完成正式原画导入；夹具使用BattleResult.EndedAt、完整套装注册表、大剑正式支持的3级及两张同级合并。日志三项专项均通过，证据output/adventurer-journal/verify-complete.log；天启内容不变。 | Fixed |
| BUG-288 | 2026-10-12 | GemGloveChecks.UpgradeAndSockets / 全量验证 | 全量334/335，满孔不替换检查失败。 | 原断言把五孔数组existing再Append第五孔，期望数组长度变成6，与实际五孔永远不相等。 | 发放经验前冻结完整五孔数组，再比较发放后全部孔位与随机状态；保持真实满孔不替换、不消耗随机数的检查语义。 | Fixed |
| BUG-289 | 2026-10-12 | CardDisplayAdapter.Rules / 升级词条重复分支 | 内容构建时报CS8510不可达switch模式。 | 工作区新增英雄升级能力时重复添加PassiveOnHeroLevelUp文案分支。 | 移除重复项，保留唯一升级与拾取分类；构建0警告0错误，全量336/336通过。 | Fixed |

| BUG-290 | 2026-10-12 | 宝石手套初稿 / CardDisplayAdapter 与 GemGloveChecks | 首次构建出现重复升级switch分支和验证引用不存在的AbilityKey；定义初稿也遗漏冷却属性。 | 文案分支插入位置重复匹配，验证混淆事件字段；冷却只写入能力未写入属性。 | 去重升级分支，按来源发动次数验证战斗排除；各级属性补齐CooldownTicks=60。最终构建0警告0错误，336/336回归通过，原生预览显示6秒与五孔。 | Fixed |

| BUG-291 | 2026-10-12 | HTML / 宝石手套样例按钮 | 点击新卡示例后详情内容生成但浮层立即隐藏。 | 点击冒泡至document通用关闭监听器，刚打开的浮层被关闭。 | 示例按钮停止冒泡，沿用卡牌点击的已有处理方式；不改变其他入口。 | Fixed |
| BUG-292 | 2026-10-13 | ShopCardPoolService / 商品生成与刷新 | 商店只生成初始等级商品，玩家已有4级卡牌仍会刷出同名卡牌。 | 商品创建未传入抽取等级，候选筛选未检查库存，刷新复用进入商店时的候选。 | 首批及刷新从初始等级至商店上限的支持等级中等概率抽取，按实际等级定价；筛除库存中的4级及以上同key卡牌，刷新前再次过滤并校验候选数量。等级抽取、价格、固定种子、满级过滤及刷新失败原子性专项通过；构建0警告0错误，全量340/340通过。 | Fixed |
| BUG-293 | 2026-10-13 | CardDisplayAdapter / 月影喵新光环展示 | 首次全量验证340/342，新相邻标签冷却增加效果使等级预览抛出NotSupportedException。 | 新通用效果已接入战斗求值，遗漏短描述与详情格式分支。 | 补齐两处通用展示格式，复用标签名称与Tick转秒规则；由全卡等级文案与预览回归覆盖。 | Fixed |
| BUG-294 | 2026-10-13 | EncounterScheduler / 宝石商店等级覆盖 | 等级覆盖只改变宝石商店候选的Level，ShopLevel仍采用定义等级，可能出现访问等级与商店上限不一致。 | ToChoice为Level和ShopLevel使用不同的等级分支。 | 两字段统一读取本次访问等级；初始等级负责解锁且不允许降级，普通遭遇提升上限4级，初始5级保持特殊等级；新增宝石商店一致性及等级边界验证。 | Fixed |
| BUG-295 | 2026-10-13 | 黑市典当铺初稿 / CardBattleState | 首轮全量371/372，既有固定战斗回放状态基线改变。 | 为价值光环把价值写入每张卡的通用战斗属性字典，导致无关回放多出字段。 | 来源价值改为独立FrozenValue，只用于价值光环与效果预览；保留既有状态字段，固定战斗基线恢复，全量372/372通过。 | Fixed |
| BUG-296 | 2026-10-13 | 黑市典当铺卡面截图入口 | 卡面保存成功，但延迟调用记录Task转Godot Variant异常。 | 异步lambda被推断为返回Task的Callable泛型委托。 | 明确转换为Action异步委托，避免向Godot返回Task；重新构建及截图复查。 | Fixed |
| BUG-297 | 2026-10-15 | PortraitClip / RoundIcon | Godot英雄、遭遇和技能头像颜色偏暗、偏浓，与原画及HTML不一致。 | fragment输入COLOR已含纹理采样，又与texture(TEXTURE, UV)相乘，重复改变RGB。 | 仅对COLOR.a应用圆形遮罩，RGB保持引擎采样结果；原生截图复查。 | Fixed |
| BUG-298 | 2026-10-15 | ContextAvatar / BattleStage / PortraitClip | 上方头像覆盖金属框内缘，局部超出内孔。 | 90px控件使用接近90px的可见圆，而360×132底座的实际内孔更小。 | 上下大头像统一使用局部坐标遮罩，90px控件内裁为72px圆，保留金属内缘。 | Fixed |
| BUG-299 | 2026-10-15 | MatchShell / StageNavigation | 左上商店、遭遇、战斗文字与横线重叠。 | HBox顶栏受82px子控件最小高度撑开，分隔线随其实际高度落进导航文字区域。 | 分隔线固定在导航下沿逻辑y=100，保留各项选中下划线。 | Fixed |
| BUG-300 | 2026-10-15 | PlayerHeroPanel / PlayerProgressPanel | 底部成长、技能及资源栏与头像金属底座两翼重叠。 | 两栏仅避开头像圆面，没有避开宽360px的底座装饰。 | HTML先将左右栏分别移至160／796px，Godot同步资源、成长、技能与入口，底座与栏边保留16px间隙。 | Fixed |
| BUG-301 | 2026-10-15 | HtmlParityCapture / 主布局证据 | 原生主布局截图仍使用旧示例阵容，且主布局与边界专项相互覆盖截图；战斗夹具保留商店头像。 | 夹具未随HTML主布局示例更新，使用同一输出目录，战斗投影未替换上下文插画。 | 主布局采用正式定义生成的当前三尺寸组合，输出至独立overview目录，战斗使用野猪头像；原有小型长数字和长详情专项保留。 | Fixed |
| BUG-302 | 2026-10-15 | UI位图导入 / CardFace / MatchShell | 卡牌、头像和金属边框缩小时出现明显锯齿与细节闪烁。 | UI位图未生成mipmap，多倍缩小仍直接采样原图；子变换独立取整还会使框图错位。 | UI的170份PNG导入统一生成mipmap，卡面与界面启用线性mipmap采样；关闭非像素画界面的变换强制取整；五档Forward+截图复查。 | Fixed |
| BUG-303 | 2026-10-15 | MatchTheme.DrawPortraitMount / PortraitClip | 头像框变形、圆孔与头像偏移，横翼不能与桌面边框接合。 | 将透明图片裁切后强制拉成360×132，改变原比例；以透明画布中点代替实际圆孔中心定位。 | 使用原图等比缩放，皮肤标定圆孔中心，头像遮罩按实际像素宽度抗锯齿；底部圆心移至逻辑y=837，横翼与桌面下沿衔接。 | Fixed |
| BUG-304 | 2026-10-15 | MatchTheme.Font / CardPreviewView | 右键放大后卡面数字与文字明显模糊，不同分辨率下质量不一致。 | 卡面整体缩放放大了既有字号的位图字形，未使用适合任意比例的字体渲染。 | 共享字体改用64px MSDF字形，界面及独立卡面启用随缩放过采样；五档窗口复查三尺寸预览。 | Fixed |
| BUG-305 | 2026-10-15 | PlayerHeroPanel / BattleStageView | 魔法条未显示每秒魔法再生。 | 只渲染当前与最大魔法，遗漏独立再生文字。 | 己方与敌方魔法条右端增加紫色+数值；构筑读取英雄快照，回放读取对应冻结HeroBattleSnapshot。 | Fixed |
| BUG-306 | 2026-10-15 | MatchPresenter / BattleStageView / PvP遭遇 | 异步对战使用竞技场插画代替实际敌方英雄。 | 遭遇图使用定义静态图；上方头像优先使用ContextIllustration，未与实际对手身份绑定。 | 生成PvP候选时请求并保留本批对手，候选与准备／回放复用英雄插画；双方英雄共用头像取景，怪物保留正式插画；来源计数与身份一致性回归通过。 | Fixed |

| BUG-307 | 2026-10-16 | EncounterSelectionView | 遭遇图卡额外显示商店／其他遭遇等级文字，视觉不统一。 | 角标按ShopLevel分支生成不同文案。 | HTML先移除等级角标，原生同步移除，保留统一等级边框。 | Fixed |
| BUG-308 | 2026-10-16 | EncounterScheduler.VisitLevel | 第一轮校场等普通遭遇可能出现4级。 | 轮次上限仅用于商店，其他遭遇仍抽到4级。 | 所有普通遭遇统一按轮次限制访问等级，保留显式测试覆盖。 | Fixed |
| BUG-309 | 2026-10-16 | ShopCardPoolService.Refresh | 刷新可能再次出现刷新前同key卡牌。 | 刷新从完整候选池抽取，未排除旧商品。 | 按刷新前整批商品key排除，重新过滤满级库存；不足3张只展示实际数量，无新卡时禁止扣费刷新。 | Fixed |

| BUG-310 | 2026-10-16 | CardEffectRow._Draw | 图鉴滚轮后部分灼伤、治疗及多重徽标底板变白。 | 强制GC及真实滚轮复现：绘制时临时StyleBoxTexture未持久持有，底板纹理随资源回收失效。 | 由组件保存底板样式及纹理，绘制复用；同条件修复前白块、修复后语义配色正常，构建0警告0错误。 | Fixed |
| BUG-311 | 2026-10-16 | 原画导入 / project-star-card-artwork | 原画规则未要求mipmap验收，破晓之门及毒宴新娘仍关闭mipmap。 | 导入检查未纳入原画接入步骤。 | 补齐两张原画mipmap并重新导入，技能增加配置、实际加载纹理及缩小／放大显示检查；萤火吉丁虫运行时确认10层mipmap。 | Fixed |
| BUG-312 | 2026-10-16 | CardDisplayAdapter.Rules | 魔能盾详情重复显示“获得护甲0”，且累计消耗描述遗漏包含本次消耗。 | 通用描述逐项输出累计魔法护甲及自身护甲加成，未合并同一能力的两项效果。 | 按通用效果组合合并为“机制公式 + 当前加成”，补全本次消耗规则；涵盖零值与负值，保留回放效果索引。 | Fixed |

| BUG-313 | 2026-10-18 | SkillCatalogView / 技能详情 | 技能详情受后方列表边框影响。 | 详情遮罩ZIndex为1，低于列表独立等级边框的绝对层级，且详情未显式使用不透明底板。 | 遮罩提升至20并设置不透明详情底板，己方技能单独打开详情并归还焦点；1600×900与1280×720原生截图确认无后方边框穿透，全量419/419通过。 | Fixed |

| BUG-314 | 2026-10-18 | CardRelatedEffectsView / 同步开发 | 文本包含“出售消耗品”时，首次关联框多显示出售与消耗；同级召唤引用显示初始等级效果。 | 全文子串匹配混淆触发词条与条件，引用统一取初始快照。 | 触发词条按规则开头匹配；同级引用按当前等级选择正式快照，机制与引用名称分离匹配。截图复核交易卡及同级引用。 | Fixed |

| BUG-315 | 2026-10-18 | HeroDetailsView.ShowSkill / 同步开发 | 新增单技能入口首次打开位于左上角。 | 复用ClampTo只限制坐标，未像ShowSection一样设置居中初始位置。 | 打开时在布局完成后居中，并增加独立背景遮罩；原生截图复核。 | Fixed |

| BUG-316 | 2026-10-18 | CardCatalogView / CardPreviewView / SkillCatalogView | 同步后的布局与HTML有明显差距，图鉴露出游戏边缘。 | 图鉴保留外边距，全部筛选被挪到左栏；原生容器强制拉满规则框，卡面与技能图标仍沿用旧尺寸。 | 先把HTML图鉴改全屏，再同步原生全屏边界、标签栏／分类行／页脚、138px卡面、预览列比例与内容高度、技能紧凑标题区；原生截图和全屏几何断言复核。 | Fixed |
| BUG-317 | 2026-10-18 | 图鉴与预览布局修订开发 | 首轮截图出现Count重复挂载、技能重挂后重复断开信号、初始卡面高度为0及倾斜被重排重置。 | 布局迁移未使用Reparent；技能监听只绑定于Ready；卡面高度计算依赖尚未排出的高度，逐帧赋值触发重排；关闭按钮TopLevel未指定前景层级。 | 使用Reparent、EnterTree/ExitTree对称监听；按卡牌比例先确定高度，尺寸实际变化时才赋值并通过事件排版；关闭按钮设为前景。捕获验证三尺寸卡面、倾斜和回正通过。 | Fixed |

| BUG-318 | 2026-10-18 | CardPreviewView / CardPreviewArt / CardFace | 原生大卡尺寸与HTML不一致，左下数值过小或不完整。 | 预览随1600×900画布缩放；透视纹理用减小卡面尺寸来留边，改变实际比例；完整大卡仍使用紧凑13/15px效果字号。 | 预览按屏幕像素排版，沿用HTML尺寸公式；纹理独立增加24px留白并通过UV比例保持卡面真实尺寸，预览效果行23px／36px高和20px图标。三尺寸正常及倾斜截图、字号和纹理边界断言通过，构建无警告错误，全量419/419通过。 | Fixed |

| BUG-319 | 2026-10-18 | CardPreviewView / CardFace / CardElementBadge | 大屏中整组详情占屏偏小，左上套装文字与右上元素也偏小。 | 上轮改用屏幕像素后仍固定1400px组宽；预览未覆盖套装／元素的紧凑尺寸，元素符号固定15px。 | HTML先增加基于1600×900的整体响应放大，再同步原生；套装15px／30px高、元素30px与自适应符号、孔位13px，透视视口按整体倍率渲染。2240×1260原生截图及组宽≥80%、角标尺寸、三尺寸卡面、倾斜回正断言通过；全量419/419通过。 | Fixed |
| BUG-320 | 2026-10-18 | HTML / 怪物图鉴卡牌提示 | 新增怪物行中的卡牌提示会落在模态图鉴后方。 | 悬停提示只识别卡牌图鉴容器，没有识别新增的world-atlas。 | 将提示挂入当前怪物图鉴模态层，关闭分类时收回并隐藏；原型脚本及阵容资源检查通过，浏览器视觉待验收。 | Fixed |

| BUG-321 | 2026-10-18 | GameLobbyView.LayoutScene | 首次打开营地时场景被整屏青色／金色反馈覆盖。 | 节点入树时尺寸为0，提前将场景容器缩放为0，导致子控件绘制变换异常。 | 跳过未获得有效宽高的布局，等待真实视口尺寸后计算缩放；恢复通用按钮反馈后重新捕获，场景、入口与焦点均正常。 | Fixed |

| BUG-322 | 2026-10-18 | MatchShell / AtlasHub | 图鉴分类页上残留大厅图鉴按钮的悬停轮廓。 | 大厅保持显示，其按钮反馈层叠加Z值高于分类页。 | 打开分类页时隐藏大厅，关闭分类页再恢复大厅及图鉴焦点，避免底层装饰穿透。 | Fixed |

| BUG-323 | 2026-10-18 | MatchShell / 大厅选角 | 已有旅程返回大厅再浏览英雄时，旧阶段组件或展开备战区可能覆盖／遮住选角。 | 新导航仅显示英雄组件，未收起备战或隐藏同宿主中的旧阶段视图。 | 进入大厅收起备战及旧提示，选角时隐藏同宿主其他阶段组件，继续旅程通过原快照恢复；捕获断言覆盖旧阶段组件不穿透。 | Fixed |

| BUG-324 | 2026-10-18 | MatchShell.OpenSettings | 大厅点击设置后窗口可见但取消等按钮无法点击。 | 设置节点在大厅之前创建；Godot的鼠标命中依据场景树顺序，ZIndex仅改变视觉，后创建的全屏大厅仍截获鼠标。 | 打开设置前将其移至同级节点末尾，使输入与视觉模态层级一致；真实鼠标取消、Tab、Escape和预览恢复回归及原生截图通过。 | Fixed |

| BUG-325 | 2026-10-18 | PlaytestVerification.RenderedScene | 新内容改变商店抽样后，训练事件刷新检查对刺客匕首误报失败。 | 断言把基础攻击值当作卡面最终伤害，忽略首次发动倍率。 | 保留事件前后成长断言，显示检查改为比对同一只读快照的最终伤害。 | Fixed |
| BUG-326 | 2026-10-18 | CardDisplayAdapter / 元素计数治疗 | 堕天使生成规则出现“治疗己方英雄己方存活战场……”的重复措辞。 | 通用治疗目标插入与新计数公式正文衔接不完整。 | 公式改为“治疗等同于……”，由目标生成器插入被治疗英雄，保留计数、系数和当前加成。 | Fixed |

| BUG-327 | 2026-10-18 | CardDisplayAdapter / 独立卡牌预览 | 野猪独立预览显示0伤害，详情公式未标当前攻击。 | 将缺少真实战斗快照一律视为缺少全部依据，丢弃已知攻击及默认满血语义。 | 生命比例独立预览默认100%，英雄等级沿用快照；详情列明当前攻击，真实冻结施加量继续优先。覆盖四级野猪、攻击加成与真实生命比例。 | Fixed |

| BUG-328 | 2026-10-18 | HeroDetailsView / 技能详情及二级弹窗 | 技能详情可见但关闭按钮被遮罩拦截。 | 全屏Shade是弹窗子节点并设TopLevel，Godot优先命中该独立输入根，即使视觉Z层在内容下方也会盖住按钮。 | 打开时将遮罩移为弹窗前的同级节点，弹窗排到末尾；同步显隐与销毁，技能和其他二级弹窗共用。真实鼠标复现命中Shade；修复后1600×900及2240×1260命中Close，关闭、底层阻断、Tab、Escape及焦点恢复通过。 | Fixed |

| BUG-329 | 2026-10-19 | ComponentShowcase.CaptureLobby / 后台截图 | 隐藏窗口的营地捕获等待FramePostDraw后不再前进。 | 隐藏或屏幕外窗口可能停止自动绘帧，捕获夹具依赖可见窗口绘帧信号。 | 营地捕获在布局帧完成后显式RenderingServer.ForceDraw(false)，再读取真实GPU视口；隔离副本保持no_focus及屏幕外位置。三尺寸截图和交互捕获通过。 | Fixed |
| BUG-330 | 2026-10-19 | ComponentShowcase.CaptureLobby / 插图验证 | 已有实际透明通道的角色被误报缺少透明插图。 | CompressedTexture2D.HasAlpha在引擎中固定返回false；无头dummy渲染也不提供实际GPU纹理数据。 | 真实渲染检查GetImage().DetectAlpha与mipmap，无头仅检查资源加载及交互；六张透明插图的GPU检查通过。 | Fixed |
| BUG-331 | 2026-10-20 | CardBattleState / 百分比属性状态初始化 | 独立部署使用百分比属性施加状态的卡牌时，非攻击属性基值可能丢失，导致灼伤或中毒为0；召唤路径却能正常加载。 | Runtime仅初始化固定属性状态的引用属性，遗漏ApplySourceAttributePercentStatusEffectDefinition，召唤路径额外复制全部属性掩盖了差异。 | 百分比属性状态同样注册并读取冻结输入中的引用属性；暴怒熊独立部署与召唤、四级内支持等级双方状态和加成验证覆盖。 | Fixed |
| BUG-332 | 2026-10-20 | SkillCatalogChecks.Interaction / 初始等级筛选夹具 | 全量验证报“技能图鉴交互失败：初始等级筛选”。 | 夹具硬编码最高初始等级只能筛出至圣斩一项，现有4级复生仪式也进入候选，数量假设已失效。 | 2026-10-21与BUG-344一并修复：按正式Definition投影检查全部初始等级候选、顺序及详情支持等级；修复前失败、修复后专项通过，证据output/bug-344/。 | Fixed |
