namespace TreeGraph.Shared.NodeEavSky.Dtos;

/// <summary>
/// 位置属性（location）运行时值：经纬度坐标 + 标记信息，用于地图导航定位节点。
///
/// 坐标系统一使用 GCJ-02（火星坐标），与高德 / 腾讯 / 谷歌中国一致，
/// 导航链接可直接使用，无需转换；跳转百度时由前端转换为 BD-09。
/// </summary>
public sealed class EavLocationValue
{
    /// <summary>纬度（GCJ-02），范围 [-90, 90]。</summary>
    public double Latitude { get; set; }

    /// <summary>经度（GCJ-02），范围 [-180, 180]。</summary>
    public double Longitude { get; set; }

    /// <summary>地点名称（导航终点 / 地图标记标题，可选）。</summary>
    public string? Label { get; set; }

    /// <summary>地址文本（可选，仅作展示记录）。</summary>
    public string? Address { get; set; }

    /// <summary>坐标系标识，当前固定 "gcj02"，预留多坐标系扩展。</summary>
    public string CoordinateSystem { get; set; } = "gcj02";
}

/// <summary>逆地理编码结果（GET api/eav/geo/regeo）。</summary>
/// <param name="Label">最近 POI 名称（可选，作为地点名称建议）。</param>
/// <param name="Address">结构化地址文本。</param>
public sealed record RegeoResultDto(string? Label, string? Address);
