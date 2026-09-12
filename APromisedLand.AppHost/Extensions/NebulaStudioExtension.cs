using Microsoft.Extensions.Hosting;

namespace APromisedLand.AppHost.Extensions;

public static class NebulaStudioExtension
{
    public static IDistributedApplicationBuilder AddNebulaStudio(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // 添加 Studio 容器
        var studio = builder.AddContainer("nebula-studio", "vesoft/nebula-graph-studio:v3.8.0")
            .WithHttpEndpoint(port: 7001, targetPort: 7001, name: "studio-http")
            .WithEnvironment("STUDIO_PORT", "7001"); // 显式声明，默认即为 7001

        // 如果有 Graphd 资源，等待它启动后再启动 Studio
        if (resourceContext.NebulaGraph != null)
        {
            studio.WaitFor(resourceContext.NebulaGraph);
        }

        // 保存到 resourceContext 供其他地方使用（可选）
        resourceContext.NebulaStudio = studio;

        return builder;
    }
}