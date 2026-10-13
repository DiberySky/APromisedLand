namespace TreeGraph.Blazor.Shared.NodeEavSky;

/// <summary>
/// WGS-84 → GCJ-02（火星坐标）转换。
///
/// 浏览器 / WebView 的 Geolocation API 返回 WGS-84 坐标，
/// 本系统位置值统一存 GCJ-02（与高德 / 腾讯地图一致），
/// 自动获取的坐标需经此转换后再保存。
/// 中国境外坐标不偏移，原样返回。
/// </summary>
public static class Gcj02Converter
{
    //  Krasovsky 1940 椭球长半轴
    private const double A = 6378245.0;
    // 椭球偏心率平方
    private const double Ee = 0.00669342162296594323;

    /// <summary>WGS-84 转 GCJ-02；境外坐标原样返回。</summary>
    public static (double Latitude, double Longitude) Wgs84ToGcj02(double latitude, double longitude)
    {
        if (OutOfChina(latitude, longitude))
            return (latitude, longitude);

        var dLat = TransformLat(longitude - 105.0, latitude - 35.0);
        var dLng = TransformLng(longitude - 105.0, latitude - 35.0);
        var radLat = latitude / 180.0 * Math.PI;
        var magic = Math.Sin(radLat);
        magic = 1 - Ee * magic * magic;
        var sqrtMagic = Math.Sqrt(magic);
        dLat = (dLat * 180.0) / (A * (1 - Ee) / (magic * sqrtMagic) * Math.PI);
        dLng = (dLng * 180.0) / (A / sqrtMagic * Math.Cos(radLat) * Math.PI);
        return (latitude + dLat, longitude + dLng);
    }

    private static bool OutOfChina(double latitude, double longitude) =>
        longitude is < 72.004 or > 137.8347 || latitude is < 0.8293 or > 55.8271;

    private static double TransformLat(double x, double y)
    {
        var ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y
                  + 0.2 * Math.Sqrt(Math.Abs(x));
        ret += (20.0 * Math.Sin(6.0 * x * Math.PI) + 20.0 * Math.Sin(2.0 * x * Math.PI)) * 2.0 / 3.0;
        ret += (20.0 * Math.Sin(y * Math.PI) + 40.0 * Math.Sin(y / 3.0 * Math.PI)) * 2.0 / 3.0;
        ret += (160.0 * Math.Sin(y / 12.0 * Math.PI) + 320 * Math.Sin(y * Math.PI / 30.0)) * 2.0 / 3.0;
        return ret;
    }

    private static double TransformLng(double x, double y)
    {
        var ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y
                  + 0.1 * Math.Sqrt(Math.Abs(x));
        ret += (20.0 * Math.Sin(6.0 * x * Math.PI) + 20.0 * Math.Sin(2.0 * x * Math.PI)) * 2.0 / 3.0;
        ret += (20.0 * Math.Sin(x * Math.PI) + 40.0 * Math.Sin(x / 3.0 * Math.PI)) * 2.0 / 3.0;
        ret += (150.0 * Math.Sin(x / 12.0 * Math.PI) + 300.0 * Math.Sin(x / 30.0 * Math.PI)) * 2.0 / 3.0;
        return ret;
    }
}
