## v1.0.7
- Repackaged under a new version number; README trimmed / README 精简，重新打包发布
- Mod version bumped to 1.0.7 / 版本号升至 1.0.7

## v1.0.6
- New building: **Forest Creator Town Hall** — 3×3 footprint, 15 wood, buildable from the start, no recipes or worker slots. Its model and icon reuse the game's own Holy Temple so it looks native in the build menu / 新增建筑「城镇大厅」：3×3 占地、造价 15 木材、开局可建、无配方与工人位。外观与图标复用游戏原生的「圣殿」，在建造菜单里不突兀
- Clicking the Town Hall opens a recruitment menu, skinned entirely after the game's own popup (same frame, gold corner ornaments, centred gold title, inset race cards) / 点击城镇大厅弹出招募菜单，窗体完全照游戏原生弹窗复刻：同样的描边、四角金色藤蔓、居中金色标题、内嵌种族卡片
- Each card = one race + 2 amber + a recruit button: 2 amber spawns one villager of that race immediately, and the button is disabled when you cannot afford it / 每张卡片 = 一个种族 + 2 琥珀 + 招募按钮：花 2 琥珀立刻招募一名该种族居民，琥珀不足时按钮不可用
- The menu skin (window frame, button, card face, card header, avatar ring) now ships as PNG inside `Assets/` and is loaded from disk at startup — no runtime harvesting from live game popups any more, so it looks identical on every map and every save / 菜单皮肤（窗体、按钮、卡片底纹、卡片标题条、头像圆环）全部随 mod 以 PNG 分发，启动时从 Assets 读取，不再在运行时从游戏弹窗抓图，任何地图、任何存档都长一个样
- Two new custom cornerstones: **Forest Creator's Fervent Worship** (villagers never leave out of low resolve) and **Forest Creator's Sacred Protection** (villagers never die from accidents) / 两个新增自定义基石：「森林造物主的狂热崇拜」（居民不会因不满离开）与「森林造物主的神圣庇佑」（居民不会因异常死亡）
- Diagnostic popup probe removed from the shipped build / 打包版移除了临时的弹窗诊断探针
- Mod version bumped to 1.0.6 / 版本号升至 1.0.6

## v1.0.5
- Fertile Soil (Biome Grass Spawner) now supports drag-box batch placement: hold the mouse and drag a rectangle to lay down a whole patch of fertile soil in one go / 沃土（肥沃田地）支持鼠标拖框批量建造：按住鼠标拖出一个方框，一次铺满一整片沃土
- Homestead (自耕农场) unlocked and available in the Food Production category of the build menu. It is injected into the in-game unlocked-building pool only — MetaSave is never written, so existing settlements keep loading normally / 解锁自耕农场，出现在建造菜单的「食物生产」分类。它只被注入到本局的已解锁建筑池，不写入元存档，因此既有定居点照常读取
- Ancient Hearth production bonus by level: gathering and farming buildings produce ×2 at hearth level 1, ×5 at level 2, ×10 at level 3 (configurable) / 古老火塘按等级提供产出倍率：1 级 ×2、2 级 ×5、3 级 ×10（可在配置文件中调整）
- Decoration resolve: every race gains extra resolve from decorations by footprint and rarity — green +1, blue +2, gold +3 per tile (configurable) / 装饰建筑按占地与稀有度提供满意度：绿色 +1/格、蓝色 +2/格、金色 +3/格（可调）
- Farming buildings: the build menu is no longer reduced to just Farmfield and Field Kitchen — the whole planting set is offered again at game start / 修复种植菜单被筛到只剩「田地」和「野外厨房」的问题，开局种植建筑全部恢复可选
- Defeat/abandon no longer advances the cycle year: AddYears() is hard-blocked after a failed run, so repeated retries can no longer inflate cycle progress / 失败或放弃不再推进循环年份：失败后 AddYears() 被直接拦截，反复重开不会再把循环进度顶满
- Mod version bumped to 1.0.5 / 版本号升至 1.0.5

## v1.0.4
- Free upgrades extended to every upgradable building (workshops and other production buildings included); the upgrade button is always clickable and no cost row is shown / 升级免费扩展到所有可升级建筑（含作坊等生产类建筑），升级按钮始终可点，面板不再显示材料需求
- Enabling rainpunk no longer costs pipes / 给建筑启用雨朋克不再消耗管道
- The rainpunk unlock button no longer shows the pipe count and icon / 雨朋克解锁按钮不再显示管道数量和图标
- The four primitive camps (small forager / herbalist / trapper / fishing hut) are hidden from the build menu — display-layer only, no save data is modified / 四个低级采集营地不再出现在建造菜单，纯显示层拦截、不改存档
- Houses of the same race and model stay upgradeable to the next level after a sync upgrade / 住房同步升级后仍可继续升到下一级
- Starting goods list: the Hauling Cart cornerstone is no longer offered as a pick (it is injected at game start instead) / 开局可选项不再提供「搬运车」基石（改为开局直接注入）
- Mod version bumped to 1.0.4 / 版本号升至 1.0.4

## v1.0.3
- Free upgrades for mines, geyser pumps and every racial house — no materials consumed, and the cost row is hidden in the upgrade panel / 矿山、间歇泉泵、所有种族住房升级全部免费，升级面板不再显示材料需求
- Housing sync upgrade: upgrading one house also upgrades every house of the same race and model / 住房同步升级：升级一处住房后同种族同型号住房同步升级
- Coverage doubled: hearth hubRange x2 and woodcutter camp maxDistance x2 / 覆盖范围 ×2：火塘覆盖范围、伐木工营地采集范围翻倍
- Robot production multiplier x5 (output amount) / 机器人产出倍率 ×5
- Robot production rate x2, i.e. production time halved / 机器人生产速度 ×2，生产时间减半
- All tunable numbers moved into a BepInEx config file (BepInEx/config/ForestCreator.cfg) with bilingual CN/EN comments / 所有可调数值移入 BepInEx 配置文件，中英双语注释
- Mod version bumped to 1.0.3 / 版本号升至 1.0.3

## v1.0.2
- Auto-inject starting cornerstones (Hauling Cart, Double Transport, Accidental Barrels, Amber for Wood, Woodcutting speed for water, Lumber Speed +50, Villager for glade) directly at game start / 开局自动注入7个基石（搬运车、双重运输、过于勤劳的木工、伐木税、强劲之水、强化斧、迷失荒野）
- Add pipe & fertilizer to starting goods options (1 cost, 10 each) / 开局可选项新增管道、肥料（1费10个）
- Top up main warehouse goods to 200 at game start / 开局主仓库已有物品补足到200
- Auto-unlock "Fertile Soil" (Biome Grass Spawner) building / 自动解锁沃土（肥沃田地）
- Robot deployment now costs 2 pipes / 机器人部署消耗2个管道
- Cheats: forest hostility -1337 at start, impatience x0.1337, woodcutter walk +13.37 / work x13.37, rewards x13.37 / 作弊增强：敌意-1337、急躁×0.1337、伐木工超快、奖励×13.37
- Debug mode: abandoning/losing no longer writes defeat records (no history popup, no profile stats, no skull on world map, no cycle year progress) / 调试模式：放弃/失败不产生失败记录
- Debug/Release conditional compilation: diagnostic logs stripped from Release builds / Debug/Release 条件编译，Release 版自动剥离诊断日志

## v1.0.1
- Embark: auto-unlock all gathering/farming buildings & the advanced rain catcher; remove them from the starting offer / 开局：自动解锁采集营地、高级集雨器、所有种植建筑；开局可选项移除采集/种植建筑
- Add the golden "Hauling Cart" cornerstone to the starting offer / 开局可选项新增金色基石「搬运车」
- Robot-worked buildings now receive the rainpunk engine production bonus (automatons were skipped due to no-crit) / 机器人工作的建筑产出享受雨朋克发动机加成

## v1.0.0
- Initial release / 首次发布
- Housing capacity x10 / 住房容量 ×10
- Embark: 100 starting points, up to 27 starting picks, extra starting goods (plant fiber, herbs, sea essence, copper bars, insect/dye conveyor) / 开局：100 点数、可选上限 27、额外初始物品
- Rain collectors continuously placeable; movable trading post / 集雨器连续放置；商栈可移动
- Trade route limit raised to 10 / 贸易路线上限 10
- Robot worker slot on the races radial menu for workshops, service buildings, collectors, mines / 工人轮盘机器人槽（车间、服务建筑、集雨器、矿井）
- Hearth corruption resistance: +1500 per level (was +150); decorations add resistance by footprint (green +50/tile, blue +150/tile, gold +300/tile) / 火塘抗性每级 +1500；装饰建筑按占地提供抗性
- Crash guards for actor task-stack edge cases / 崩溃护栏
