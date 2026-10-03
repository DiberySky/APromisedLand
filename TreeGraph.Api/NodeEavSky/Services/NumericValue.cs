namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// 数量类型运行时值：数值 + 单位。
/// UnitId 为 null 时表示按属性基准单位输入。
/// </summary>
public readonly record struct NumericValue(decimal Value, Guid? UnitId = null);
