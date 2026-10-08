using Aspire.Hosting;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraph.Blazor(EAV 管理台 Blazor Server)的 Aspire 编排扩展。
/// 固定主机端口 5783,通过服务发现调用 TreeGraphEavApi + TreeGraphFileStorageApi,不直连数据库。
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

        // ★ TreeGraphFileStorageApi 可选:TreeGraph() 中先于 Blazor 声明,
        //   但 AddSeaweedFs 等任一前置失败时可能为 null。null 时跳过引用,
        //   不抛异常,让 EavApi 单独可用(用于仅做 EAV 调试的场景)。
        var blazor = builder
            .AddProject<Projects.TreeGraph_Blazor>("treegraphblazor")
            .WithHttpEndpoint(port: TreeGraphBlazorHttpPort, name: "http")
            .WithExternalHttpEndpoints()
            .WithReference(context.TreeGraphEavApi)
            .WaitFor(context.TreeGraphEavApi);

        if (context.TreeGraphFileStorageApi is not null)
        {
            blazor
                .WithReference(context.TreeGraphFileStorageApi)
                .WaitFor(context.TreeGraphFileStorageApi);
        }

        context.TreeGraphBlazor = blazor;
        blazor.WithOtlpExporter();

        return builder;
    }
}
