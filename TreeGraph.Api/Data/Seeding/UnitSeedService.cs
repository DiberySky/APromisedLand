using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.NodeEavSky.Services;

namespace TreeGraph.Api.NodeEavSky.Data.Seeding;

/// <summary>
/// 单位种子服务：固定 GUID，运行时一次性写入（幂等）。
///
/// ★ GUID 在本文件内显式指定（非随机），便于跨环境引用一致、
///   便于种子数据与外部系统对齐。
///
/// 排除的分类（有意为之）：
///   - 温度（°C / °F / K）：非线性换算（+273.15），
///     而 UnitConverter 只做乘法，纳入会导致数据损坏。
///   - 货币（CNY / USD / ...）：汇率动态，不能用固定 ToBaseFactor。
///
/// 注意：若数据库已存在旧单位（随机 GUID），本 seed 会因 AnyAsync()
/// 直接跳过，不会替换。切换到固定 GUID 需先清空 units 表：
///   DELETE FROM attribute_values WHERE unit_id IS NOT NULL;  -- 若被引用
///   DELETE FROM composite_field_definitions WHERE unit_id IS NOT NULL;
///   UPDATE attribute_catalog SET unit_id = NULL;
///   DELETE FROM units;
///   然后重启 API。
/// </summary>
public class UnitSeedService
{
    private readonly EavDbContext _db;
    private readonly IUnitCache _unitCache;
    private readonly ILogger<UnitSeedService> _logger;

    public UnitSeedService(
        EavDbContext db, IUnitCache unitCache, ILogger<UnitSeedService> logger)
    {
        _db = db;
        _unitCache = unitCache;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // 幂等：已有数据则跳过
        if (await _db.Units.AnyAsync(ct))
        {
            _logger.LogInformation("单位表已有数据，跳过 seed");
            return;
        }

        var units = BuildUnits();
        _db.Units.AddRange(units);
        await _db.SaveChangesAsync(ct);
        _unitCache.Invalidate();

        _logger.LogInformation("已 seed {Count} 个单位（固定 GUID）", units.Count);
    }

    /// <summary>
    /// 构建单位清单：13 个分类、50 个单位。GUID 显式固定。
    /// </summary>
    private static List<Unit> BuildUnits()
    {
        var units = new List<Unit>();
        var now = DateTimeOffset.UtcNow;

        // ==================== 长度 length（基准：米） ====================
        AddCategory(units, "length", baseSymbol: "m", now,
            ("c0a1b2c3-d4e5-4f6a-7b8c-9d0e1f2a3b4c", "米",     "m",   1.0m,          1),
            ("d1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "千米",   "km",  1000m,         2),
            ("e2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e", "厘米",   "cm",  0.01m,         3),
            ("f3d4e5f6-a7b8-4c9d-0e1f-2a3b4c5d6e7f", "毫米",   "mm",  0.001m,        4),
            ("d7b8c9d0-e1f2-4a3b-4c5d-6e7f8a9b0c1d", "英寸",   "in",  0.0254m,       5),
            ("c6a7b8c9-d0e1-4f2a-3b4c-5d6e7f8a9b0c", "英尺",   "ft",  0.3048m,       6),
            ("b5f6a7b8-c9d0-4e1f-2a3b-4c5d6e7f8a9b", "码",     "yd",  0.9144m,       7),
            ("a4e5f6a7-b8c9-4d0e-1f2a-3b4c5d6e7f8a", "英里",   "mi",  1609.344m,     8)
        );

        // ==================== 重量 weight（基准：千克） ====================
        AddCategory(units, "weight", baseSymbol: "kg", now,
            ("e8c9d0e1-f2a3-4b4c-5d6e-7f8a9b0c1d2e", "千克", "kg", 1.0m,             1),
            ("f9d0e1f2-a3b4-4c5d-6e7f-8a9b0c1d2e3f", "克",   "g",  0.001m,           2),
            ("a0e1f2a3-b4c5-4d6e-7f8a-9b0c1d2e3f4a", "毫克", "mg", 0.000001m,        3),
            ("b1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a5b", "吨",   "t",  1000m,            4),
            ("c2a3b4c5-d6e7-4f8a-9b0c-1d2e3f4a5b6c", "磅",   "lb", 0.45359237m,      5),
            ("d3b4c5d6-e7f8-4a9b-0c1d-2e3f4a5b6c7d", "盎司", "oz", 0.028349523125m,  6)
        );

        // ==================== 体积 volume（基准：升） ====================
        AddCategory(units, "volume", baseSymbol: "L", now,
            ("2d21c35a-4251-479e-b814-060b2fc84445", "立方米", "m³", 1000m,  1),
            ("7f0af6a9-ba1a-469c-b967-e32afe43cad2", "升",     "L",  1.0m,   2),
            ("780e7a01-350d-45ec-b963-b36a996de614", "毫升",   "mL", 0.001m, 3)
        );

        // ==================== 面积 area（基准：平方米） ====================
        AddCategory(units, "area", baseSymbol: "m²", now,
            ("e88b04db-40ac-4bb7-b420-1f3b37180673", "平方米",   "m²", 1.0m,        1),
            ("0d7ebe17-93ae-4e4c-92f4-063a124cd181", "平方公里", "km²", 1000000m,  2),
            ("a4c312d3-023e-4d4e-b5a7-fb7fcbd55c56", "公顷",     "ha", 10000m,     3),
            ("fefa26a5-d608-411c-b637-469a886e558c", "亩",       "亩", 666.6666667m, 4)
        );

        // ==================== 时间 time（基准：秒） ====================
        AddCategory(units, "time", baseSymbol: "s", now,
            ("e4c5d6e7-f8a9-4b0c-1d2e-3f4a5b6c7d8e", "秒",   "s",   1.0m,    1),
            ("f5d6e7f8-a9b0-4c1d-2e3f-4a5b6c7d8e9f", "分钟", "min", 60m,     2),
            ("a6e7f8a9-b0c1-4d2e-3f4a-5b6c7d8e9f0a", "小时", "h",   3600m,   3),
            ("b7f8a9b0-c1d2-4e3f-4a5b-6c7d8e9f0a1b", "天",   "d",   86400m,  4)
        );

        // ==================== 速度 speed（基准：米/秒） ====================
        AddCategory(units, "speed", baseSymbol: "m/s", now,
            ("4cbec89d-3f52-4db3-9ab0-faeeb841ffbf", "米/秒",      "m/s",  1.0m,          1),
            ("64e918fb-ee9d-45c7-b35a-2a55f5a5fe62", "千米/小时",  "km/h", 0.2777777778m, 2),
            ("405ae7a3-8a13-479d-bc1a-6f9d3c15e521", "英里/小时",  "mph",  0.44704m,      3)
        );

        // ==================== 角度 angle（基准：度） ====================
        AddCategory(units, "angle", baseSymbol: "°", now,
            ("ed1b66d2-454b-453b-9d43-12605dffa456", "度",   "°",   1.0m,              1),
            ("3d9088a3-7283-4f8f-b995-b193a57a6c2a", "弧度", "rad", 57.29577951308232m, 2)
        );

        // ==================== 电流 current（基准：安培） ====================
        AddCategory(units, "current", baseSymbol: "A", now,
            ("f1d2e3f4-a5b6-4c7d-8e9f-0a1b2c3d4e5f", "安培", "A",  1.0m,      1),
            ("a2e3f4a5-b6c7-4d8e-9f0a-1b2c3d4e5f6a", "毫安", "mA", 0.001m,    2),
            ("b3f4a5b6-c7d8-4e9f-0a1b-2c3d4e5f6a7b", "微安", "µA", 0.000001m, 3)
        );

        // ==================== 电压 voltage（基准：伏特） ====================
        AddCategory(units, "voltage", baseSymbol: "V", now,
            ("c4a5b6c7-d8e9-4f0a-1b2c-3d4e5f6a7b8c", "伏特", "V",  1.0m,   1),
            ("d5b6c7d8-e9f0-4a1b-2c3d-4e5f6a7b8c9d", "千伏", "kV", 1000m,  2),
            ("e6c7d8e9-f0a1-4b2c-3d4e-5f6a7b8c9d0e", "毫伏", "mV", 0.001m, 3)
        );

        // ==================== 功率 power（基准：瓦特） ====================
        AddCategory(units, "power", baseSymbol: "W", now,
            ("f7d8e9f0-a1b2-4c3d-4e5f-6a7b8c9d0e1f", "瓦特", "W",  1.0m,              1),
            ("a8e9f0a1-b2c3-4d4e-5f6a-7b8c9d0e1f2a", "千瓦", "kW", 1000m,             2),
            ("b9f0a1b2-c3d4-4e5f-6a7b-8c9d0e1f2a3b", "兆瓦", "MW", 1000000m,          3),
            ("a00be966-be2e-484e-92b6-9706494ac775", "马力", "hp", 745.6998715822702m, 4)
        );

        // ==================== 压力 pressure（基准：帕斯卡） ====================
        AddCategory(units, "pressure", baseSymbol: "Pa", now,
            ("221d3c45-911f-4e94-9c3e-c13e6f2bcc76", "帕斯卡", "Pa",  1.0m,      1),
            ("883a9940-84ec-4daa-8448-609461b984ea", "千帕",   "kPa", 1000m,     2),
            ("d5306eb8-324f-4088-a867-6fbc7141fd59", "兆帕",   "MPa", 1000000m,  3),
            ("8981bd9a-bd99-4f4c-b8af-038975b799be", "巴",     "bar", 100000m,   4)
        );

        // ==================== 能量 energy（基准：焦耳） ====================
        AddCategory(units, "energy", baseSymbol: "J", now,
            ("5db494d7-2a9c-4ae0-86e0-d4bb0dfc7b81", "焦耳",   "J",   1.0m,       1),
            ("279a6b18-6d01-4437-b95b-0480ca7adc98", "千焦",   "kJ",  1000m,      2),
            ("e3c1f025-3b2b-461a-a5a1-015cb4e3fe38", "千瓦时", "kWh", 3600000m,   3)
        );

        // ==================== 频率 frequency（基准：赫兹） ====================
        AddCategory(units, "frequency", baseSymbol: "Hz", now,
            ("5e880060-9410-40d7-bcb4-545ccd0c1bb6", "赫兹", "Hz",  1.0m,      1),
            ("1a80ed3b-1b36-4d8b-b80b-3070dbc7979d", "千赫", "kHz", 1000m,     2),
            ("41572712-95dd-4caf-b316-e1b924bc57c3", "兆赫", "MHz", 1000000m,  3)
        );

        // ★ 有意排除：
        //   - 温度：非线性换算（°C ↔ K = ±273.15），UnitConverter 只做乘法
        //   - 货币：汇率动态，不能用固定 ToBaseFactor
        //
        // 若未来需要，应在扩展 UnitConverter 支持 affine 变换（offset + factor）
        // 后再纳入。参见：UnitsController / UnitConverter。

        return units;
    }

    /// <summary>
    /// 为一个分类添加单位。
    /// 基准单位（symbol == baseSymbol）自动标记 IsBaseUnit = true。
    /// </summary>
    private static void AddCategory(
        List<Unit> units, string category, string baseSymbol,
        DateTimeOffset now,
        params (string Id, string Name, string Symbol, decimal Factor, int Order)[] items)
    {
        foreach (var (id, name, symbol, factor, order) in items)
        {
            units.Add(new Unit
            {
                Id = Guid.Parse(id),
                Category = category,
                Name = name,
                Symbol = symbol,
                ToBaseFactor = factor,
                IsBaseUnit = symbol == baseSymbol,
                DisplayOrder = order,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }
}
