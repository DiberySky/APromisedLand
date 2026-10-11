namespace TreeGraph.Api.NodeEavSky.Services;

/// <summary>
/// 数量类型运行时值：数值 + 单位。
/// UnitId 指向 unit_trees 节点 Id（string），仅作记录不参与换算。
/// </summary>
public readonly record struct NumericValue(decimal Value, string? UnitId = null);
