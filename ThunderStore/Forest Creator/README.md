# ForestCreator

《风暴之城》(Against the Storm) 的超级作弊 Mod，让你像森林造物主一样掌控一切，高难度的游戏也会变得超级简单。

A super-cheat mod for Against the Storm. Master everything like the Forest Creator himself — even the hardest difficulty becomes trivially easy.

---

## 功能 
## Features

### 住房 
### Housing
- 住房容量 ×10（GetHouseCapacity ×10）

  Housing capacity ×10

### 开局 
### Embark
- 起始点数 100，初始可选上限 27

  100 starting points, up to 27 picks

- 初始可选额外物品：植物纤维、香草、海洋精华、铜条、昆虫输送线、染料输送线、管道、肥料；另加可选组合「1 费 10 个管道」「1 费 10 个肥料」

  Extra starting goods: plant fibre, herbs, sea essence, copper bars, insect conveyor, dye conveyor, pipes, fertilizer; plus two 1-point picks giving 10 pipes or 10 fertilizer

- **开局可选项移除所有采集/种植建筑**：采集营地（觅食/草药/设阱/渔屋/收割/石匠营地）与种植建筑（小农场/种植园/香草园/卤水作坊）的蓝图不再占用开局拾取

  All gathering and farming buildings are removed from the starting offer, so they never take up a pick

- **开局自动解锁采集/种植建筑**：正式采集营地、高级集雨器、沃土（肥沃田地）、所有种植建筑开局直接可建造（加入基础建筑解锁池，无需蓝图或拾取）

  Auto-unlock all gathering camps, the advanced rain collector, fertile soil, and every farming building — buildable from the start without blueprints or picks

- **开局自动注入基石**（不占可选槽，直接生效）：搬运车、双重运输系统、过于勤劳的木工、伐木税、强劲之水、强化斧(+50)、迷失荒野

  Seven cornerstones applied automatically at game start without taking a pick slot: Hauling Cart in All Warehouses, 2 Hauling Carts in Main Warehouse, Accidental Barrels, Amber for Wood, Woodcutting speed for water, Lumber Speed +50, Villager for glade

- **开局主仓库物品补足到200**：开局时仓库已有物品数量自动补到200（超过200的不动）

  Every item already in the main warehouse is topped up to 200 at game start (items above 200 are left alone)

### 建筑 
### Buildings
- 集雨器、高级集雨器连续放置

  Rain collectors and advanced rain collectors can be placed continuously

- 商栈可移动

  Trading posts can be moved

- **低级采集营地从建造列表移除**：小型觅食者营地、小型草药医生营地、小型设阱捕兽者营地、小型捕鱼小屋不再出现在建造菜单（纯显示层拦截，不改动任何存档数据，老档照旧能开）

  The four primitive camps (small forager / herbalist / trapper / fishing hut) are hidden from the build menu. Purely a display-layer filter — no save data is touched, so existing settlements still load

- **建筑升级全部免费**：矿山、间歇泉泵、住房、作坊等所有可升级建筑，升级不消耗任何材料，升级面板也不再显示材料需求，升级按钮始终可点

  Every upgradable building — mines, geyser pumps, houses, workshops and the rest — upgrades for free: no materials are spent and the cost row is gone from the panel, so the upgrade button is always clickable

- **住房同步升级**：升级某一处住房后，同种族同型号的其他住房会同步升到相同等级，第二级同样可以继续升

  Housing sync upgrade: upgrading one house also upgrades every house of the same race and model, and the next level stays available

- **启用雨朋克免费**：给建筑启用雨朋克引擎不再消耗管道，解锁按钮上也不再显示管道数量和图标

  Enabling rainpunk on a building costs no pipes any more, and the unlock button no longer prints the pipe count or its icon

### 覆盖范围 
### Coverage
- **火塘覆盖范围 ×2**（hubRange 翻倍，服务范围更大）

  Hearth coverage radius ×2, so a single hearth serves a much larger area

- **伐木工营地采集范围 ×2**（maxDistance 翻倍，一棵营地能覆盖更大林地）

  Woodcutter camp gathering radius ×2

### 贸易 
### Trade
- 贸易路线上限 10（原版 3）

  Trade route limit raised to 10 (vanilla is 3)

### 机器人 
### Robots
- 工人轮盘新增「机器人」槽：车间、服务建筑（酒馆/诊所/教堂等）、集雨器、矿井等支持机器人的建筑可直接放置机器人自动生产

  A "Robot" slot is added to the worker radial menu, letting workshops, service buildings, rain collectors and mines take an automaton directly

- 不支持的建筑不显示机器人槽

  The robot slot is hidden on unsupported buildings

- 机器人槽不显示「推荐工种」角标

  No "recommended worker" badge on the robot slot

- **机器人工作建筑的产出享受雨朋克发动机加成**（自动化工种原本因「不能暴击」被跳过建筑级加成，已修复）

  Buildings worked by an automaton now receive the rainpunk engine production bonus (automatons used to be skipped because they cannot crit)

- **机器人产出倍率 ×5**：机器人完成配方时，产出数量是原配方的 5 倍

  Robot output ×5 — an automaton produces five times the recipe's base amount

- **机器人生产时间减半**：机器人的生产速度 ×2，做完一个配方只需原版一半时间

  Robot production time halved — automaton production speed is ×2

- **机器人部署消耗2个管道**（计数器按仓库管道数量显示可部署次数）

  Deploying a robot costs 2 pipes, and the counter shows how many you can still place

### 作弊增强 
Cheats
- **压制森林敌意**：开局直接 -1337 敌意，森林几乎不主动攻击

  Forest hostility -1337 at game start, so the forest almost never attacks

- **降低女王急躁率**：急躁成长速度 ×0.1337，极慢

  Queen's impatience growth ×0.1337, extremely slow

- **伐木工超快**：走路速度 +13.37，工作速度 ×13.37，砍树飞快

  Woodcutters walk at +13.37 speed and work at ×13.37 speed

- **结算奖励倍率**：每日跑/常规跑奖励 ×13.37

  Rewards from daily and regular runs ×13.37

### 失败无记录 
No Defeat Records

- 局中放弃/失败**不产生任何失败记录**：不写游戏历史失败弹窗、不记 profile 统计、大地图底部不出现骷髅失败城镇、cycle 年数进度条不上涨（胜利正常结算）

  Abandoning or losing a run writes no defeat record at all: no history popup, no profile stats, no skull town on the world map, and the cycle year progress bar does not advance (wins settle normally)

### 火塘腐蚀抗性 
Hearth Corruption Resistance

- 火塘每升一级抗性加成 +150 → +1500

  Corruption resistance per hearth upgrade goes from +150 to +1500

- 装饰建筑按占地提供额外抗性：绿色 +50/格，蓝色 +150/格，金色 +300/格

  Decorations add resistance by footprint: green +50 per tile, blue +150 per tile, gold +300 per tile

### 稳定性 
Stability

- 一系列崩溃护栏（Actor 任务栈空栈保护等）

  A set of crash guards, including an empty actor task-stack fallback

---

## 配置文件 Config

所有可调数值已从代码抽到 BepInEx 配置文件，首次启动游戏后自动生成在：

Every tunable number now lives in a BepInEx config file, generated on first launch at:

```
BepInEx/config/ForestCreator.cfg
```

改完保存即可，多数数值下次触发对应逻辑时生效，无需重新编译。文件内每项都是「中文说明 + 英文说明」双行注释。

Save the file and the new values are picked up the next time the matching logic runs — no rebuild needed. Each entry carries a two-line comment: Chinese first, English after.

| 节 Section | 可调项 Options |
| --- | --- |
| Hostility 敌意 | 开局敌意值（默认 -1337）<br>Start hostility (default -1337) |
| Impatience 急躁 | 女王急躁成长倍率（默认 0.1337）<br>Impatience rate multiplier (default 0.1337) |
| Woodcutter 伐木工 | 行走速度加成、工作速度倍率（默认 13.37 / 13.37）<br>Walk speed bonus and work rate multiplier (default 13.37 / 13.37) |
| Rewards 结算奖励 | 每日局、常规局奖励倍率（默认 13.37）<br>Daily and regular run reward multipliers (default 13.37) |
| Housing 住房 | 住房容量倍率（默认 10）<br>House capacity multiplier (default 10) |
| Hearth 火塘 | 升级抗性倍率、装饰抗性开关、绿/蓝/金装饰每格抗性（默认 10 / true / 50 / 150 / 300）<br>Upgrade bonus multiplier, decoration resistance toggle, and per-tile resistance for green/blue/gold decorations |
| Coverage 覆盖范围 | 火塘范围倍率、伐木工营地范围倍率（默认 2 / 2）<br>Hearth and woodcutter camp range multipliers (default 2 / 2) |
| Robot 机器人 | 产出倍率、生产速度倍率、部署耗材数量（默认 5 / 2 / 2）<br>Output multiplier, production rate multiplier and deployment cost (default 5 / 2 / 2) |
| TradeRoutes 贸易路线 | 贸易路线上限（默认 10）<br>Trade route limit (default 10) |
| Embark 开局准备 | 准备点数、开局奖励上限（默认 100 / 27）<br>Preparation points and pick limit (default 100 / 27) |
| Warehouse 仓库 | 开局补足数量（默认 200）<br>Warehouse top-up target (default 200) |

数值填 1 或 0 即等于关闭对应加成，可以自己调强度。

Setting a value to 1 or 0 effectively disables that bonus, so you can tune the difficulty to your own taste.

---

## 安装 Installation

1. 使用 [r2modman](https://thunderstore.io/c/against-the-storm/) 或手动安装 BepInEx（需 BepInExPack 5.4.2100）。

   Install BepInEx with [r2modman](https://thunderstore.io/c/against-the-storm/) or manually (BepInExPack 5.4.2100 required).
2. 将 `ForestCreator.dll` 放入 `BepInEx/plugins/` 目录。

   Drop `ForestCreator.dll` into the `BepInEx/plugins/` folder.
3. 启动游戏。

   Launch the game.

## 依赖 Dependencies

- `BepInEx-BepInExPack-5.4.2100`

## 源码 Source

GitHub: https://github.com/wenquan888/ForestCreator
