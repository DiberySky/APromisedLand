namespace APromisedLand.AppHost.Extensions;

/// <summary>纯 WASM 静态宿主编排。仅向浏览器注入公开地址，不注入任何客户端密钥。</summary>
public static class TreeGraphWasmExtension
{
    public static IDistributedApplicationBuilder AddTreeGraphBlazorWasm(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        if (context.TreeGraphEavApi is null || context.Keycloak is null)
            throw new InvalidOperationException("AddTreeGraphBlazorWasm 必须先声明 TreeGraphEavApi 与 Keycloak");

        var keycloak = context.Keycloak.GetEndpoint("http");
        var wasm = builder.AddProject<Projects.TreeGraph_Blazor_WebHost>("treegraphblazorwasm")
            .WithHttpsEndpoint(port: 5787, name: "https")
            .WithExternalHttpEndpoints()
            // 显式传播宿主环境：静态宿主依赖 Development 下的静态 Web 资产清单。
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
            .WaitFor(context.TreeGraphEavApi)
            .WithEnvironment("PublicBrowser__KeycloakAuthority",
                ReferenceExpression.Create($"{keycloak}/realms/apromisedland"))
            .WithEnvironment("PublicBrowser__ClientId", "treegraph-wasm")
            .WithEnvironment("PublicBrowser__TreeGraphApi", "http://localhost:5773")
            .WithEnvironment("PublicBrowser__SyncServices", "http://localhost:5775")
            .WithEnvironment("PublicBrowser__FileStorageApi", "http://localhost:5326")
            .WithEnvironment("PublicBrowser__MafWfApi", "http://localhost:5323")
            .WithEnvironment("PublicBrowser__IdentityAdminApi", "http://localhost:5785");

        if (context.TreeGraphIdentityAdminApi is not null)
            wasm.WaitFor(context.TreeGraphIdentityAdminApi);

        context.TreeGraphBlazorWasm = wasm;
        wasm.WithOtlpExporter();
        return builder;
    }
}
