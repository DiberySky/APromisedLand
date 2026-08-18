using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost.Extensions;

public static class NebulaGraphApiServiceExtension
{
    /// <summary>
    /// 添加 NebulaGraph FastAPI Python 服务到 Aspire AppHost
    /// 使用 AddUvicornApp 将 Python FastAPI 作为一等公民资源编排
    /// </summary>
    public static IDistributedApplicationBuilder AddNebulaGraphApiService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        // 确保 NebulaGraph 容器已注册
        if (context.NebulaGraph == null)
        {
            throw new InvalidOperationException(
                "NebulaGraph container must be registered before NebulaGraphApiService. " +
                "Call builder.AddNebulaGraph(context) first.");
        }

        // ========== NebulaGraph FastAPI Python Service ==========
        // 使用 Aspire 13+ 的 AddUvicornApp 原生支持 Python ASGI 应用
        // 自动管理虚拟环境、依赖安装、端口分配、健康检查
        var nebulaApi = builder.AddUvicornApp(
                name: "nebula-graph-api",
                projectDirectory: "../NebulaGraphApiService",  // Python 项目目录
                appName: "app.main:app"                        // FastAPI app 入口
            )
            // 使用 uv 作为包管理器（推荐，比 pip 快 10-100 倍）
            .WithUv()
            // 配置 HTTP 端点，FastAPI 默认监听 8000
            .WithHttpEndpoint(name: "http", port: 8000, targetPort: 8000, env: "PORT")
            // 暴露到外部，供前端/其他服务调用
            .WithExternalHttpEndpoints()
            // 健康检查端点
            .WithHttpHealthCheck("/api/v1/health")
            // 注入 NebulaGraph 连接信息
            .WithEnvironment("NEBULA_HOSTS", $"{context.NebulaGraphEndpoint.Host}:{context.NebulaGraphEndpoint.Port}")
            .WithEnvironment("NEBULA_USER", "root")
            .WithEnvironment("NEBULA_PASSWORD", "nebula")
            .WithEnvironment("NEBULA_SPACE", "")
            .WithEnvironment("NEBULA_MAX_CONN_POOL_SIZE", "20")
            // 注入 OpenTelemetry 配置（Aspire Dashboard 自动收集 Trace/Metrics/Logs）
            .WithEnvironment("OTEL_SERVICE_NAME", "nebula-graph-api")
            .WithEnvironment("OTEL_RESOURCE_ATTRIBUTES", "service.namespace=nebula")
            // 等待 NebulaGraph 和 Console 就绪
            .WaitFor(context.NebulaGraph)
            .WaitFor(context.NebulaConsole);

        // 将 API 服务引用注入上下文，供其他服务使用
        context.NebulaGraphApi = nebulaApi;
        context.NebulaGraphApiEndpoint = nebulaApi.GetEndpoint("http");

        return builder;
    }
}
