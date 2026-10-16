using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TreeGraph.Blazor.Shared.MafWfSky;
using TreeGraph.Blazor.Shared.MafWfSky.Extensions;
using TreeGraph.Blazor.Shared.MafWfSky.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.MafWfSky;

/// <summary>AddMafWfSky 注册断言：服务发现地址推导、选项注入、客户端可解析。</summary>
public class MafWfSkyServiceCollectionTests
{
    private static ServiceProvider BuildProvider(
        string serviceName = "TreeGraphMafWfApi",
        Action<MafWfSkyOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMafWfSky(serviceName, configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddMafWfSky_注册MafWfSkyOptions单例()
    {
        using var sp = BuildProvider(configure: o => o.ChatPath = "custom/chat");

        var options = sp.GetRequiredService<MafWfSkyOptions>();
        Assert.Equal("custom/chat", options.ChatPath);
    }

    [Fact]
    public void AddMafWfSky_默认选项路由对齐后端控制器()
    {
        using var sp = BuildProvider();
        var o = sp.GetRequiredService<MafWfSkyOptions>();

        Assert.Equal("api/chat", o.ChatPath);
        Assert.Equal("api/graph-agent", o.GraphAgentPath);
        Assert.Equal("api/Agents", o.AgentsPath);
        Assert.Equal("api/Workflows", o.WorkflowsPath);
    }

    [Fact]
    public void AddMafWfSky_服务名转小写服务发现地址()
    {
        using var sp = BuildProvider("TreeGraphMafWfApi");

        // typed client 注册时基址写入 HttpClientFactory 的命名选项，通过工厂验证
        var factory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
        var client = factory.CreateClient(nameof(MafWfApiClient));

        Assert.Equal(new Uri("https+http://treegraphmafwfapi/"), client.BaseAddress);
    }

    [Fact]
    public void AddMafWfSky_完整基址直通_本地脱离Aspire()
    {
        using var sp = BuildProvider("http://localhost:5323");

        var factory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
        var client = factory.CreateClient(nameof(MafWfApiClient));

        Assert.Equal(new Uri("http://localhost:5323/"), client.BaseAddress);
    }

    [Fact]
    public void AddMafWfSky_挂弹性后客户端超时交由弹性管道管理()
    {
        using var sp = BuildProvider();

        // AddStandardResilienceHandler 接管超时后，HttpClient.Timeout 必须为 InfiniteTimeSpan，
        // 实际超时边界由弹性选项控制（非流式 Attempt 60s / Total 10min；SSE 2h 窗口）。
        var factory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
        var client = factory.CreateClient(nameof(MafWfApiClient));

        Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public void AddMafWfSky_SSE客户端无限超时()
    {
        using var sp = BuildProvider();

        var factory = sp.GetRequiredService<System.Net.Http.IHttpClientFactory>();
        var client = factory.CreateClient(nameof(MafWfSseReader));

        Assert.Equal(System.Threading.Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public void AddMafWfSky_TypedClient可解析()
    {
        using var sp = BuildProvider();

        Assert.NotNull(sp.GetService<MafWfApiClient>());
        Assert.NotNull(sp.GetService<MafWfSseReader>());
    }
}
