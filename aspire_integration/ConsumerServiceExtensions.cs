using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

public static class ConsumerServiceExtensions
{
    /// <summary>
    /// DiberyTreeService 引用 NebulaGraph Python API
    /// 自动注入服务发现环境变量
    /// </summary>
    public static IDistributedApplicationBuilder AddDiberyTreeService(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        var service = builder.AddProject<Projects.DiberyTreeService>("dibery-tree")
            .WithReference(context.Redis!)
            .WithReference(context.PostgresDb!)
            // ← 关键：引用 Python FastAPI 服务，自动注入 NEBULAGRAPHAPI_HTTP
            .WithReference(context.NebulaGraphApi!)
            .WaitFor(context.NebulaGraphApi!)
            .WaitFor(context.Redis!)
            .WaitFor(context.PostgresDb!);

        return builder;
    }

    /// <summary>
    /// MAUI 前端引用 NebulaGraph Python API
    /// </summary>
    public static IDistributedApplicationBuilder AddDiberyMauiSky(
        this IDistributedApplicationBuilder builder,
        AppHostContext context)
    {
        var maui = builder.AddProject<Projects.DiberyMauiSky>("dibery-maui")
            // 前端可直接调用 Python API
            .WithReference(context.NebulaGraphApi!)
            .WithReference(context.NebulaStudio!)
            .WaitFor(context.NebulaGraphApi!);

        return builder;
    }
}
