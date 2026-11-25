using Aspire.Hosting;
using Microsoft.Extensions.Configuration;

namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// TreeGraph.SyncServices 的 Aspire 编排扩展。
/// 独立承载搜索、向量同步/对账与图同步，固定主机端口 5775。
/// </summary>
public static class TreeGraphSyncServicesExtension
{
    private const int HttpPort = 5775;

    public static IDistributedApplicationBuilder AddTreeGraphSyncServices(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        if (context.TreeGraphDb is null)
            throw new InvalidOperationException(
                "AddTreeGraphSyncServices 前需先调用 builder.AddPostgres(context)");
        if (context.TreeGraphEavApi is null)
            throw new InvalidOperationException(
                "AddTreeGraphSyncServices 必须在 AddTreeGraphEavApi 之后调用");

        var syncServices = builder
            .AddProject<Projects.TreeGraph_SyncServices>("treegraphsyncservices")
            .WithHttpEndpoint(port: HttpPort, name: "http")
            .WithReference(context.TreeGraphDb)
            .WaitFor(context.TreeGraphDb)
            // 核心 API 负责迁移；等待其健康检查通过后再启动同步消费者。
            .WaitFor(context.TreeGraphEavApi);

        if (context.RabbitMq is not null)
        {
            syncServices
                .WithReference(context.RabbitMq)
                .WaitFor(context.RabbitMq);
        }

        if (context.Qdrant is not null)
        {
            syncServices
                .WithReference(context.Qdrant)
                .WaitFor(context.Qdrant);
        }

        if (context.VllmEmbed is not null)
        {
            var embedEndpoint = context.VllmEmbed.GetEndpoint(
                VllmExtension.EmbeddingHttpEndpointName);

            syncServices
                .WaitFor(context.VllmEmbed)
                .WithEnvironment("Embedding__Endpoint",
                    ReferenceExpression.Create($"{embedEndpoint}/v1/"));
        }

        if (context.TreeGraphNebulaFastApiEndpoint is not null)
        {
            syncServices.WithEnvironment("NebulaFastApi__Endpoint",
                ReferenceExpression.Create($"{context.TreeGraphNebulaFastApiEndpoint}/"));

            if (context.TreeGraphNebulaFastApi is not null)
                syncServices.WaitFor(context.TreeGraphNebulaFastApi);
        }

        syncServices.WireIfPresent(context.Redis);
        syncServices
            .WithEnvironment("Keycloak__Authority", "https://localhost:8323/realms/apromisedland")
            .WithEnvironment("Keycloak__Audience", "treegraph-web")
            .WithEnvironment("Keycloak__AdditionalAudiences__0", "treegraph-wasm")
            .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
            .WithEnvironment("Cors__WasmOrigins__0", "https://localhost:5787")
            .WithEnvironment("Identity__RequireAuthenticatedApi",
                builder.Configuration.GetValue<bool>("Frontend:LegacyTrafficRetired").ToString())
            .WithHttpHealthCheck(path: "/health", statusCode: 200, endpointName: "http")
            .WithOtlpExporter();

        context.TreeGraphSyncServices = syncServices;
        return builder;
    }
}
