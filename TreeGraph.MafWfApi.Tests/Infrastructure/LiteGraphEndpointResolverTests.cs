using TreeGraph.MafWfApi.Infrastructure;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Infrastructure;

/// <summary>
/// 环境变量优先级测试。同类内 xUnit 串行执行，保证环境变量不互相污染。
/// </summary>
public class LiteGraphEndpointResolverTests : IDisposable
{
    private static readonly string[] EnvVars =
    [
        "services__litegraph__http__0",
        "services__litegraph__https__0",
        "LITEGRAPH_ENDPOINT",
        "ConnectionStrings__litegraph",
    ];

    public LiteGraphEndpointResolverTests() => ClearAll();

    public void Dispose() => ClearAll();

    private static void ClearAll()
    {
        foreach (var v in EnvVars)
            Environment.SetEnvironmentVariable(v, null);
    }

    [Fact]
    public void 全部未配置_回退localhost8701()
    {
        var result = LiteGraphEndpointResolver.Resolve(new LiteGraphOptions());
        Assert.Equal("http://localhost:8701", result);
    }

    [Fact]
    public void appsettings配置_被采用()
    {
        var result = LiteGraphEndpointResolver.Resolve(
            new LiteGraphOptions { Endpoint = "http://example:9000/" });
        Assert.Equal("http://example:9000", result);
    }

    [Fact]
    public void appsettings配置_无协议自动补http()
    {
        var result = LiteGraphEndpointResolver.Resolve(
            new LiteGraphOptions { Endpoint = "example:9000" });
        Assert.Equal("http://example:9000", result);
    }

    [Fact]
    public void 服务发现环境变量_优先于appsettings()
    {
        Environment.SetEnvironmentVariable(
            "services__litegraph__http__0", "http://aspire:8701/");

        var result = LiteGraphEndpointResolver.Resolve(
            new LiteGraphOptions { Endpoint = "http://example:9000" });

        Assert.Equal("http://aspire:8701", result);
    }

    [Fact]
    public void 连接串Endpoint格式_被解析()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__litegraph", "Endpoint=http://connstr:8701");

        var result = LiteGraphEndpointResolver.Resolve(new LiteGraphOptions());

        Assert.Equal("http://connstr:8701", result);
    }

    [Fact]
    public void 连接串纯URL_直接采用()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__litegraph", "http://plainurl:8701/");

        var result = LiteGraphEndpointResolver.Resolve(new LiteGraphOptions());

        Assert.Equal("http://plainurl:8701", result);
    }

    [Fact]
    public void 服务发现_优先于连接串()
    {
        Environment.SetEnvironmentVariable(
            "services__litegraph__http__0", "http://sd:8701");
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__litegraph", "http://conn:8701");

        var result = LiteGraphEndpointResolver.Resolve(new LiteGraphOptions());

        Assert.Equal("http://sd:8701", result);
    }
}
