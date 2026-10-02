using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TreeGraph.Blazor.Tests;

/// <summary>
/// 启动级 smoke 测试：在测试宿主里跑完整的 Program.cs pipeline，
/// 触发所有 <c>ValidateOnStart</c> 的 Options 校验。
///
/// 目的：防止配置错误（如 EavApiClient 的 Resilience 参数）潜伏到运行期。
/// 背景：曾因 <c>MaxRetryAttempts = 0</c> + <c>SamplingDuration</c> 不满足
/// 约束，Aspire 编排启动时抛 OptionsValidationException，
/// 但 dotnet build 通过、单元测试也通过。
///
/// 首次构造 <see cref="BlazorAppFactory"/> 时，工厂会调用
/// <c>IHost.StartAsync()</c>，此时所有 <c>ValidateOnStart</c> 的 Options 会被校验；
/// 任何非法配置都会在测试中直接抛异常。
/// </summary>
public class StartupTests : IClassFixture<BlazorAppFactory>
{
    private readonly BlazorAppFactory _factory;

    public StartupTests(BlazorAppFactory factory) => _factory = factory;

    /// <summary>
    /// 宿主启动不抛异常即通过。工厂构造时已触发校验。
    /// </summary>
    [Fact]
    public void Host_Starts_WithValidOptions()
    {
        Assert.NotNull(_factory.Server);
    }

    /// <summary>
    /// 打通 HTTP pipeline：请求 Aspire 默认端点的 /alive。
    /// 覆盖：路由注册 + 中间件顺序 + 无意外依赖解析失败。
    /// </summary>
    [Fact]
    public async Task Health_Endpoint_Is_Reachable()
    {
        using var client = _factory.CreateClient();

        var resp = await client.GetAsync("/alive");

        // MapDefaultEndpoints 在 Development 环境注册 /alive；
        // 若注册未生效（环境判断出错），会 404，我们把它视为异常。
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}

/// <summary>
/// 测试宿主工厂：覆盖外部依赖，让 Program.cs 完整 pipeline 能在无 Aspire、
/// 无真实 DB、无 OTEL collector 的环境下启动。
/// </summary>
public sealed class BlazorAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 与生产启动保持一致的环境（影响 MapDefaultEndpoints 是否注册 /alive）
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // 假连接串：不会真连（Blazor 不注册 DbContext，测试也不触达 DB 路径），
                // 仅作为占位配置保留。
                ["ConnectionStrings:TreeGraphDb"] =
                    "Host=localhost;Port=1;Database=fake;Username=fake;Password=fake",

                // AddServiceDefaults 检测到此为空 → 不启用 OTLP exporter，
                // 避免测试环境尝试连接 collector。
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "",

                // 关闭 Aspire 服务发现探测（若存在），避免无谓的 DNS 查询。
                // EavApiClient 的 BaseAddress 只是被设置，测试不真实发请求。
                ["Aspire:ServiceDiscovery:Enabled"] = "false",
            });
        });
    }
}
