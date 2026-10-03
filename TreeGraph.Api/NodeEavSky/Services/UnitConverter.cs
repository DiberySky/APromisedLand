using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>单位换算服务：目标值 = 源值 × 源单位系数 ÷ 目标单位系数</summary>
public class UnitConverter
{
    private readonly IUnitCache _unitCache;

    public UnitConverter(IUnitCache unitCache) => _unitCache = unitCache;

    /// <summary>把值从 fromUnit 换算到 toUnit（同分类）</summary>
    public decimal Convert(decimal value, Guid fromUnitId, Guid toUnitId)
    {
        if (fromUnitId == toUnitId) return value;

        var from = _unitCache.Get(fromUnitId);
        var to = _unitCache.Get(toUnitId);

        if (from.Category != to.Category)
            throw new InvalidOperationException(
                $"跨分类换算不允许：{from.Category} → {to.Category}");

        return value * from.ToBaseFactor / to.ToBaseFactor;
    }

    /// <summary>把值换算到属性基准单位</summary>
    public decimal ToBase(decimal value, Guid inputUnitId, Guid baseUnitId)
        => Convert(value, inputUnitId, baseUnitId);

    /// <summary>从基准单位还原到指定单位</summary>
    public decimal FromBase(decimal baseValue, Guid baseUnitId, Guid targetUnitId)
        => Convert(baseValue, baseUnitId, targetUnitId);
}
