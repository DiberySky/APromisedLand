namespace APromisedLand.AppHost.Extensions;

/// <summary>
/// Keycloak 22+ 容器编排。离线运行验证暂用本机 26.6；管理员凭据与客户端密钥全部走参数，
/// realm 首次启动通过 <c>--import-realm</c> 导入版本化 JSON。
/// </summary>
public static class KeycloakExtension
{
    private const int KeycloakHttpPort = 8323;
    private const string RealmJsonFileName = "KeycloakRealmpart.json";

    public static IDistributedApplicationBuilder AddKeycloak(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        var adminUser = builder.AddParameter("Keycloak-username", "admin");
        var adminPassword = builder.AddParameter("Keycloak-password", secret: true);
        var webClientSecret = builder.AddParameter("Keycloak-web-secret", secret: true);
        var adminClientSecret = builder.AddParameter("Keycloak-admin-secret", secret: true);

        resourceContext.KeycloakUsername = adminUser;
        resourceContext.KeycloakPassword = adminPassword;
        resourceContext.KeycloakWebClientSecret = webClientSecret;
        resourceContext.KeycloakAdminClientSecret = adminClientSecret;

        var realmJsonPath = Path.Combine(AppContext.BaseDirectory, RealmJsonFileName);
        // 开发调试主题：预填测试账号密码，仅在本地调试期挂载。
        var themePath = Path.Combine(AppContext.BaseDirectory, "KeycloakThemes", "treegraph");

        resourceContext.Keycloak = builder
            .AddKeycloak("Keycloak", KeycloakHttpPort, adminUser, adminPassword)
            .WithImageTag("26.6")
            .WithDataVolume("keycloak-data")
            .WithBindMount(themePath, "/opt/keycloak/themes/treegraph", isReadOnly: true)
            .WithEnvironment("TREEGRAPH_WEB_CLIENT_SECRET", webClientSecret)
            .WithEnvironment("TREEGRAPH_ADMIN_CLIENT_SECRET", adminClientSecret)
            // Quarkus 默认单请求头上限约 50KB，本地 localhost 各端口共享 Cookie 罐，
            // 累积后会让授权端点在浏览器侧直接 431。提高到 256KB，实际使用不再触发。
            .WithEnvironment("QUARKUS_HTTP_LIMITS_MAX_HEADER_SIZE", "262144")
            // HTTP/2 另有独立的头列表上限（实测 SETTINGS 通告 8192 字节），上面的配置管不到，
            // 浏览器由此收到 431/协议错误。关闭 h2 让浏览器回落 HTTP/1.1，统一走 256KB 上限。
            .WithEnvironment("QUARKUS_HTTP_HTTP2", "false")
            .WithRealmImport(realmJsonPath)
            .WithOtlpExporter();

        return builder;
    }
}
