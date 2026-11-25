namespace APromisedLand.AppHost.Extensions;

public static class TreeGraphIdentityAdminApiExtension
{
    public static IDistributedApplicationBuilder AddTreeGraphIdentityAdminApi(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext context)
    {
        if (context.Keycloak is null || context.Redis is null || context.IdentityAdminDb is null)
            throw new InvalidOperationException("IdentityAdmin.Api 需要 Keycloak、Redis 与 IdentityAdminDb。");

        var api = builder.AddProject<Projects.TreeGraph_IdentityAdmin_Api>("treegraphidentityadminapi")
            .WithHttpEndpoint(port: 5785, name: "http")
            .WithExternalHttpEndpoints()
            // 显式传播宿主环境：保证开发期健康端点与详细错误页行为一致。
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
            .WithReference(context.Keycloak)
            .WaitFor(context.Keycloak)
            .WithReference(context.Redis)
            .WaitFor(context.Redis)
            .WithReference(context.IdentityAdminDb)
            .WaitFor(context.IdentityAdminDb)
            .WithEnvironment("Keycloak__Authority", "https://localhost:8323/realms/apromisedland")
            .WithEnvironment("Keycloak__AdminBaseUrl", "https://localhost:8323")
            .WithEnvironment("Keycloak__Realm", "apromisedland")
            .WithEnvironment("Keycloak__Audience", "treegraph-wasm")
            .WithEnvironment("Keycloak__WebClientId", "treegraph-web")
            .WithEnvironment("Keycloak__AdminClientId", "treegraph-admin")
            .WithEnvironment("Keycloak__RequireHttpsMetadata", "false")
            .WithEnvironment("Keycloak__WebBaseUrl", "http://localhost:5783")
            .WithEnvironment("Keycloak__WasmClientId", "treegraph-wasm")
            .WithEnvironment("Keycloak__WasmBaseUrl", "https://localhost:5787")
            .WithEnvironment("Cors__WasmOrigins__0", "https://localhost:5787")
            .WithEnvironment("IdentityAdmin__EnableBootstrap", "true")
            .WithEnvironment("IdentityAdmin__EnableDatabaseMigration", "true");

        if (context.KeycloakWebClientSecret is not null)
            api.WithEnvironment("Keycloak__WebClientSecret", context.KeycloakWebClientSecret);
        if (context.KeycloakAdminClientSecret is not null)
            api.WithEnvironment("Keycloak__AdminClientSecret", context.KeycloakAdminClientSecret);

        context.TreeGraphIdentityAdminApi = api;
        api.WithOtlpExporter();
        return builder;
    }
}
