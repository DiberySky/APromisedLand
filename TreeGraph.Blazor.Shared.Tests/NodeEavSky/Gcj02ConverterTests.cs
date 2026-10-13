using TreeGraph.Blazor.Shared.NodeEavSky;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.NodeEavSky;

/// <summary>Gcj02Converter：WGS-84 → GCJ-02 转换。</summary>
public class Gcj02ConverterTests
{
    [Fact]
    public void Wgs84ToGcj02_ChinaCoordinate_Shifts()
    {
        // 北京天安门 WGS-84 ≈ (39.9087, 116.3975)
        var (lat, lng) = Gcj02Converter.Wgs84ToGcj02(39.9087, 116.3975);

        // GCJ-02 结果应与输入不同（有偏移），且偏移在百米量级内
        Assert.NotEqual(39.9087, lat);
        Assert.NotEqual(116.3975, lng);
        Assert.InRange(lat, 39.905, 39.913);
        Assert.InRange(lng, 116.393, 116.404);
    }

    [Theory]
    [InlineData(39.9087, 71.0)]    // 经度过小（境外）
    [InlineData(39.9087, 138.0)]   // 经度过大（境外）
    [InlineData(0.5, 116.0)]       // 纬度过小（境外）
    [InlineData(56.0, 116.0)]      // 纬度过大（境外）
    public void Wgs84ToGcj02_OutOfChina_ReturnsUnchanged(double lat, double lng)
    {
        var (rLat, rLng) = Gcj02Converter.Wgs84ToGcj02(lat, lng);

        Assert.Equal(lat, rLat);
        Assert.Equal(lng, rLng);
    }

    [Fact]
    public void Wgs84ToGcj02_ShiftIsMeterScale()
    {
        // 偏移量应在 50~700 米之间（1 纬度 ≈ 111km）
        var (lat, lng) = Gcj02Converter.Wgs84ToGcj02(31.2304, 121.4737);

        var dLatMeters = Math.Abs(lat - 31.2304) * 111_000;
        Assert.InRange(dLatMeters, 1, 700);
    }
}
