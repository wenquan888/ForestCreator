# ForestCreator

《风暴之城》(Against the Storm) 的 BepInEx Mod，独立项目，与原版 mod 分离。

## 功能

- 住房容量 ×10
- 开局起始点数 100 / 可选上限 27
- 固定 picked 布局
- 集雨器、高级集雨器连续放置；商栈可移动
- 贸易路线上限 10
- 工人轮盘新增「机器人」槽：车间/服务/集雨器/矿井等支持机器人的建筑可直接放置机器人自动生产
  - 不支持的建筑不显示机器人槽
  - 机器人槽不显示「推荐工种」角标
- 火塘腐蚀抗性增强
  - 每升一级抗性加成 +150 → +1500
  - 装饰建筑按占地提供额外抗性：绿色 +50/格，蓝色 +150/格，金色 +300/格
- 一系列崩溃护栏（Actor 任务栈空栈保护等）

## 编译

项目已自包含，所有引用 dll 放在 `libs/`（含 publicized 的 `Assembly-CSharp_publicized.dll`），克隆后无需游戏安装即可编译：

```bash
dotnet build -c Release
```

产物：`bin/Release/netstandard2.0/ForestCreator.dll`，部署到 BepInEx 的 `plugins/` 目录。

## 结构

```
ForestCreator/
├── ForestCreator.csproj
├── Plugin.cs              # 入口
├── Diagnostics.cs         # 日志/调试
├── Patches.Housing.cs     # 住房 ×10
├── Patches.Embark.cs      # 起始点/可选上限/初始物品
├── Patches.PickedLayout.cs
├── Patches.BuildingPlacement.cs  # 连续放置/商栈移动
├── Patches.TradeRoutes.cs # 贸易上限
├── Patches.HearthCorruption.cs   # 火塘抗性增强
├── Patches.CrashGuards.cs # 崩溃护栏
├── Patches.RobotSlot.cs   # 机器人槽
├── RobotMenuSlot.cs       # 机器人槽逻辑
└── libs/                  # 引用 dll（提交）
```
