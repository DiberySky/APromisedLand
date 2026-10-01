using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Entities;
using TreeGraph.Api.Services;

namespace TreeGraph.Api.Data.Seeding;

/// <summary>单位种子服务：随机 Guid，运行时一次性写入（幂等）</summary>
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

        _logger.LogInformation("已 seed {Count} 个单位", units.Count);
    }

    /// <summary>
    /// 构建单位清单。每次调用都生成新的随机 Guid。
    /// </summary>
    private static List<Unit> BuildUnits()
    {
        var units = new List<Unit>();
        var now = DateTimeOffset.UtcNow;

        // ==================== 长度 length（基准：米） ====================
        AddCategory(units, "length", baseSymbol: "m", now,
            ("毫米", "mm", 0.001m, 1),
            ("厘米", "cm", 0.01m, 2),
            ("分米", "dm", 0.1m, 3),
            ("米", "m", 1.0m, 4),
            ("千米", "km", 1000m, 5),
            ("英寸", "in", 0.0254m, 6),
            ("英尺", "ft", 0.3048m, 7),
            ("码", "yd", 0.9144m, 8),
            ("英里", "mi", 1609.344m, 9),
            ("海里", "nmi", 1852m, 10)
        );

        // ==================== 重量 weight（基准：千克） ====================
        AddCategory(units, "weight", baseSymbol: "kg", now,
            ("毫克", "mg", 0.000001m, 1),
            ("克", "g", 0.001m, 2),
            ("千克", "kg", 1.0m, 3),
            ("吨", "t", 1000m, 4),
            ("盎司", "oz", 0.028349523125m, 5),
            ("磅", "lb", 0.45359237m, 6)
        );

        // ==================== 体积 volume（基准：升） ====================
        AddCategory(units, "volume", baseSymbol: "L", now,
            ("毫升", "mL", 0.001m, 1),
            ("厘升", "cL", 0.01m, 2),
            ("分升", "dL", 0.1m, 3),
            ("升", "L", 1.0m, 4),
            ("立方米", "m³", 1000m, 5),
            ("立方厘米", "cm³", 0.001m, 6),
            ("加仑(美)", "gal", 3.785411784m, 7),
            ("加仑(英)", "gal(UK)", 4.54609m, 8)
        );

        // ==================== 面积 area（基准：平方米） ====================
        AddCategory(units, "area", baseSymbol: "m²", now,
            ("平方毫米", "mm²", 0.000001m, 1),
            ("平方厘米", "cm²", 0.0001m, 2),
            ("平方米", "m²", 1.0m, 3),
            ("平方千米", "km²", 1000000m, 4),
            ("公顷", "ha", 10000m, 5),
            ("亩", "亩", 666.6666667m, 6),
            ("平方英尺", "ft²", 0.09290304m, 7),
            ("英亩", "ac", 4046.8564224m, 8)
        );

        // ==================== 时间 time（基准：秒） ====================
        AddCategory(units, "time", baseSymbol: "s", now,
            ("毫秒", "ms", 0.001m, 1),
            ("秒", "s", 1.0m, 2),
            ("分钟", "min", 60m, 3),
            ("小时", "h", 3600m, 4),
            ("天", "d", 86400m, 5),
            ("周", "wk", 604800m, 6)
        );

        // ==================== 速度 speed（基准：米/秒） ====================
        AddCategory(units, "speed", baseSymbol: "m/s", now,
            ("米/秒", "m/s", 1.0m, 1),
            ("千米/小时", "km/h", 0.2777777778m, 2),
            ("英里/小时", "mph", 0.44704m, 3),
            ("节", "kn", 0.5144444444m, 4)
        );

        // ==================== 数据大小 data（基准：字节） ====================
        AddCategory(units, "data", baseSymbol: "B", now,
            ("字节", "B", 1m, 1),
            ("千字节", "KB", 1024m, 2),
            ("兆字节", "MB", 1048576m, 3),
            ("吉字节", "GB", 1073741824m, 4),
            ("太字节", "TB", 1099511627776m, 5)
        );

        // ==================== 角度 angle（基准：度） ====================
        AddCategory(units, "angle", baseSymbol: "°", now,
            ("度", "°", 1.0m, 1),
            ("弧度", "rad", 57.29577951308232m, 2),
            ("分", "'", 0.01666666667m, 3),
            ("秒", "\"", 0.00027777778m, 4)
        );

        return units;
    }

    /// <summary>
    /// 为一个分类添加单位，基准单位自动标记 IsBaseUnit = true
    /// </summary>
    private static void AddCategory(
        List<Unit> units, string category, string baseSymbol,
        DateTimeOffset now,
        params (string Name, string Symbol, decimal Factor, int Order)[] items)
    {
        foreach (var (name, symbol, factor, order) in items)
        {
            units.Add(new Unit
            {
                Id = Guid.NewGuid(), // 随机 Guid
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
