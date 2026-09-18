using MAFWorkFlowApi.Infrastructure;
using MAFWorkFlowApi.Services;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>注册 LiteGraph 图数据服务（REST 直连，跳过 SDK）。</summary>
    public static IServiceCollection AddLiteGraph(
        this IServiceCollection services, IConfiguration configuration)
    {
        // ─── 1. 配置绑定 ────────────────────────────────
        services
            .AddOptions<LiteGraphOptions>()
            .Bind(configuration.GetSection(LiteGraphOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // ─── 2. 命名 HttpClient（专用超时 + 无 Aspire 弹性管道）──
        services.AddHttpClient("LiteGraph", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        });

        // ─── 3. REST 客户端 ─────────────────────────────
        services.AddSingleton<LiteGraphRestClient>();

        // ─── 4. 业务服务 ────────────────────────────────
        services.AddSingleton<NodeAuthoringService>();
        services.AddSingleton<EdgeAuthoringService>();

        return services;
    }
}