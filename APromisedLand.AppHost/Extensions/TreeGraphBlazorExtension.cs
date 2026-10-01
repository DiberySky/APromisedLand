using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraph.Blazor(EAV 管理台 Blazor Server)的 Aspire 编排扩展。
/// 固定主机端口 5783,通过服务发现调用 TreeGraphEavApi,不直连数据库。
/// </summary>
public static class TreeGraphBlazorExtension
{
    private const int TreeGraphBlazorHttpPort = 5783;

    public static IDistributedApplicationBuilder AddTreeGraphBlazor(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        if (context.TreeGraphEavApi is null)
            throw new InvalidOperationException(
                "AddTreeGraphBlazor 必须在 AddTreeGraphEavApi 之后调用");

        context.TreeGraphBlazor = builder
            .AddProject<Projects.TreeGraph_Blazor>("treegraphblazor")
            .WithHttpEndpoint(port: TreeGraphBlazorHttpPort, name: "http")
            .WithExternalHttpEndpoints()
            .WithReference(context.TreeGraphEavApi)
            .WaitFor(context.TreeGraphEavApi);

        context.TreeGraphBlazor.WithOtlpExporter();

        return builder;
    }
}
