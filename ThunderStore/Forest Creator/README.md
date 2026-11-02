# ForestCreator

《风暴之城》(Against the Storm) 的 BepInEx Mod。提供一系列 QoL 增强与自定义玩法。

A BepInEx mod for Against the Storm with a set of QoL and customization features.

---

## 功能 Features

### 住房 Housing
- 住房容量 ×10（GetHouseCapacity ×10）

### 开局 Embark
- 起始点数 100，初始可选上限 27
- 初始可选额外物品：植物纤维、香草、海洋精华、铜条、昆虫输送线、染料输送线等

### 建筑 Building
- 集雨器、高级集雨器连续放置
- 商栈可移动

### 贸易 Trade
- 贸易路线上限 10（原版 3）

### 机器人 Robot Worker
- 工人轮盘新增「机器人」槽：车间、服务建筑（酒馆/诊所/教堂等）、集雨器、矿井等支持机器人的建筑可直接放置机器人自动生产
- 不支持的建筑不显示机器人槽
- 机器人槽不显示「推荐工种」角标

### 火塘腐蚀抗性 Hearth Corruption Resistance
- 火塘每升一级抗性加成 +150 → +1500
- 装饰建筑按占地提供额外抗性：绿色 +50/格，蓝色 +150/格，金色 +300/格

### 稳定性 Stability
- 一系列崩溃护栏（Actor 任务栈空栈保护等）

---

## 安装 Installation

1. 使用 [r2modman](https://thunderstore.io/c/against-the-storm/) 或手动安装 BepInEx（需 BepInExPack 5.4.2100）。
2. 将 `ForestCreator.dll` 放入 `BepInEx/plugins/` 目录。
3. 启动游戏。

## 依赖 Dependencies

- `BepInEx-BepInExPack-5.4.2100`

## 源码 Source

GitHub: https://github.com/wenquan888/ForestCreator
