using BepInEx.Configuration;

namespace ForestCreator
{
    /// <summary>
    /// 本模组所有可调数值的集中配置（All tunable values for this mod）。
    /// 数值保存在 BepInEx/config/ForestCreator.cfg，每次补丁执行时都会重新读取，
    /// 因此改完配置基本可以立即生效，不需要重新编译模组。
    /// </summary>
    public static class FCConfig
    {
        // ---- 敌意 / Hostility ----
        public static ConfigEntry<int> StartHostility;

        // ---- 急躁 / Impatience ----
        public static ConfigEntry<float> ImpatienceMultiplier;

        // ---- 伐木工 / Woodcutter ----
        public static ConfigEntry<float> WoodcutterWalkBonus;
        public static ConfigEntry<float> WoodcutterWorkMultiplier;

        // ---- 结算奖励 / Rewards ----
        public static ConfigEntry<float> DailyRewardMultiplier;
        public static ConfigEntry<float> RegularRewardMultiplier;

        // ---- 住房 / Housing ----
        public static ConfigEntry<int> HouseCapacityMultiplier;

        // ---- 火塘 / Hearth ----
        public static ConfigEntry<bool> EnableDecorationResistance;
        public static ConfigEntry<int> HearthUpgradeBonusMultiplier;
        public static ConfigEntry<int> DecorationGreenPerTile;
        public static ConfigEntry<int> DecorationBluePerTile;
        public static ConfigEntry<int> DecorationGoldPerTile;

        // ---- 火塘等级产出加成 / Hearth level production bonus ----
        public static ConfigEntry<int> HearthLevel1ProductionMultiplier;
        public static ConfigEntry<int> HearthLevel2ProductionMultiplier;
        public static ConfigEntry<int> HearthLevel3ProductionMultiplier;

        // ---- 装饰满意度 / Decoration resolve ----
        public static ConfigEntry<int> DecorationGreenResolvePerTile;
        public static ConfigEntry<int> DecorationBlueResolvePerTile;
        public static ConfigEntry<int> DecorationGoldResolvePerTile;

        // ---- 覆盖范围 / Coverage ----
        public static ConfigEntry<float> HearthRangeMultiplier;
        public static ConfigEntry<float> CampRangeMultiplier;

        // ---- 机器人 / Robot ----
        public static ConfigEntry<int> RobotProductionMultiplier;
        public static ConfigEntry<float> RobotProductionRateMultiplier;
        public static ConfigEntry<int> RobotDeployPartsCost;

        // ---- 贸易路线 / Trade Routes ----
        public static ConfigEntry<int> MaxTradeRoutes;

        // ---- 开局准备 / Embark ----
        public static ConfigEntry<int> EmbarkPreparationPoints;
        public static ConfigEntry<int> EmbarkMaxPicks;

        // ---- 仓库 / Warehouse ----
        public static ConfigEntry<int> WarehouseTopUpTarget;

        public static void Init(ConfigFile config)
        {
            StartHostility = config.Bind("Hostility 敌意", "Start Hostility 开局敌意",
                -1337,
                "每局开始时为森林增加的敌意值，负数为压制敌意，正值会增加敌意。\n" +
                "Hostility added at the start of every game (negative = calm the forest).");

            ImpatienceMultiplier = config.Bind("Impatience 急躁", "Impatience Rate Multiplier 女王急躁成长倍率",
                0.1337f,
                "女王急躁值的增长速度乘以此倍率，数值越小急躁涨得越慢。\n" +
                "How much the Queen's impatience growth rate is multiplied by.");

            WoodcutterWalkBonus = config.Bind("Woodcutter 伐木工", "Walk Speed Bonus 行走速度加成",
                13.37f,
                "伐木工职业村民的行走速度固定加成值。\n" +
                "Flat walking speed bonus for woodcutter-profession villagers.");

            WoodcutterWorkMultiplier = config.Bind("Woodcutter 伐木工", "Work Rate Multiplier 工作速度倍率",
                13.37f,
                "伐木工职业建筑的生产速度倍率。\n" +
                "Production rate multiplier for woodcutter-profession buildings.");

            DailyRewardMultiplier = config.Bind("Rewards 结算奖励", "Daily Run Reward Multiplier 每日局奖励倍率",
                13.37f,
                "完成每日局后获得货币的倍率。\n" +
                "Multiplies currency earned after completing a daily run.");

            RegularRewardMultiplier = config.Bind("Rewards 结算奖励", "Regular Run Reward Multiplier 常规局奖励倍率",
                13.37f,
                "完成常规局后获得货币的倍率。\n" +
                "Multiplies currency earned after completing a regular run.");

            HouseCapacityMultiplier = config.Bind("Housing 住房", "House Capacity Multiplier 住房容量倍率",
                10,
                "所有住房建筑的人口容量乘以此倍率。\n" +
                "Multiplies the population capacity of every housing building.");

            HearthUpgradeBonusMultiplier = config.Bind("Hearth 火塘", "Upgrade Bonus Multiplier 升级抗性倍率",
                10,
                "火塘每升一级获得的腐蚀抗性乘以此倍率，游戏原版每级为 150。\n" +
                "Multiplies the corruption resistance gained per hearth upgrade level (game default is 150 per level).");

            EnableDecorationResistance = config.Bind("Hearth 火塘", "Decoration Grants Resistance 装饰建筑提供抗性",
                true,
                "设为 true 时，装饰类建筑会按占地格数提供额外腐蚀抗性。\n" +
                "If true, decorative buildings add corruption resistance based on their footprint.");

            DecorationGreenPerTile = config.Bind("Hearth 火塘", "Green Decoration Per Tile 绿色装饰每格抗性",
                50,
                "绿色（普通）装饰建筑每格提供的腐蚀抗性。\n" +
                "Corruption resistance added per tile for green/common decorations.");

            DecorationBluePerTile = config.Bind("Hearth 火塘", "Blue Decoration Per Tile 蓝色装饰每格抗性",
                150,
                "蓝色（稀有）装饰建筑每格提供的腐蚀抗性。\n" +
                "Corruption resistance added per tile for blue/rare decorations.");

            DecorationGoldPerTile = config.Bind("Hearth 火塘", "Gold Decoration Per Tile 金色装饰每格抗性",
                300,
                "金色（传奇）装饰建筑每格提供的腐蚀抗性。\n" +
                "Corruption resistance added per tile for gold/legendary decorations.");

            HearthLevel1ProductionMultiplier = config.Bind("Hearth 火塘", "Level 1 Production Multiplier 1级产出倍率",
                2,
                "古老火塘为 1 级时，采集与种植建筑（营地、渔屋、农场、集雨器）产出数量的倍率。\n" +
                "Output multiplier for gathering and farming buildings while the Ancient Hearth is at level 1.");

            HearthLevel2ProductionMultiplier = config.Bind("Hearth 火塘", "Level 2 Production Multiplier 2级产出倍率",
                5,
                "古老火塘为 2 级时，采集与种植建筑产出数量的倍率。\n" +
                "Output multiplier for gathering and farming buildings while the Ancient Hearth is at level 2.");

            HearthLevel3ProductionMultiplier = config.Bind("Hearth 火塘", "Level 3 Production Multiplier 3级产出倍率",
                10,
                "古老火塘为 3 级时，采集与种植建筑产出数量的倍率。\n" +
                "Output multiplier for gathering and farming buildings while the Ancient Hearth is at level 3.");

            DecorationGreenResolvePerTile = config.Bind("Decoration 装饰", "Green Resolve Per Tile 绿色装饰每格满意度",
                1,
                "绿色（普通）装饰建筑每格为所有种族提供的满意度。\n" +
                "Resolve added for every race per tile of green/common decorations.");

            DecorationBlueResolvePerTile = config.Bind("Decoration 装饰", "Blue Resolve Per Tile 蓝色装饰每格满意度",
                2,
                "蓝色（稀有）装饰建筑每格为所有种族提供的满意度。\n" +
                "Resolve added for every race per tile of blue/rare decorations.");

            DecorationGoldResolvePerTile = config.Bind("Decoration 装饰", "Gold Resolve Per Tile 金色装饰每格满意度",
                3,
                "金色（传奇）装饰建筑每格为所有种族提供的满意度。\n" +
                "Resolve added for every race per tile of gold/legendary decorations.");

            HearthRangeMultiplier = config.Bind("Coverage 覆盖范围", "Hearth Range Multiplier 火塘范围倍率",
                2f,
                "所有火塘的覆盖半径乘以此倍率。\n" +
                "Multiplies the coverage radius of every hearth.");

            CampRangeMultiplier = config.Bind("Coverage 覆盖范围", "Woodcutter Camp Range Multiplier 伐木工营地范围倍率",
                2f,
                "所有伐木工营地的采集半径乘以此倍率。\n" +
                "Multiplies the gathering radius of every woodcutter camp.");

            RobotProductionMultiplier = config.Bind("Robot 机器人", "Production Multiplier 产出倍率",
                5,
                "机器人工作时，产出数量的倍率。\n" +
                "Multiplies the output amount when an automaton does the production.");

            RobotProductionRateMultiplier = config.Bind("Robot 机器人", "Production Rate Multiplier 生产速度倍率",
                2f,
                "机器人的生产速度倍率，数值越大生产时间越短。\n" +
                "Multiplies the production speed (i.e. shortens production time) for automatons.");

            RobotDeployPartsCost = config.Bind("Robot 机器人", "Deployment Parts Cost 部署耗材数量",
                2,
                "部署一个机器人需要消耗的管道数量。\n" +
                "How many Pipes are consumed when deploying an automaton.");

            MaxTradeRoutes = config.Bind("TradeRoutes 贸易路线", "Max Trade Routes 贸易路线上限",
                10,
                "同时可进行的贸易路线数量下限，游戏原版为 3。\n" +
                "Minimum number of simultaneous trade routes allowed (game default is 3).");

            EmbarkPreparationPoints = config.Bind("Embark 开局准备", "Preparation Points 准备点数",
                100,
                "开局前的准备点数，游戏原版为 10。\n" +
                "Preparation (embark) points available before starting a game (game default is 10).");

            EmbarkMaxPicks = config.Bind("Embark 开局准备", "Max Picks 开局奖励上限",
                27,
                "开局界面最多可同时选中的开局奖励数量，游戏原版为 9。\n" +
                "Maximum number of opening rewards that can be picked at once on the embark screen (game default is 9).");

            WarehouseTopUpTarget = config.Bind("Warehouse 仓库", "Top Up Target 开局补足数量",
                200,
                "开局时，主仓库中已有的每种物品补足到至少这个数量。\n" +
                "Every item already present in the main warehouse is topped up to at least this amount at game start.");
        }
    }
}
