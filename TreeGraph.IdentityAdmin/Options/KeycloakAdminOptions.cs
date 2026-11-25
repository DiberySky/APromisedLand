namespace TreeGraph.IdentityAdmin.Options;

/// <summary>
/// Keycloak 身份管理平台配置，绑定配置节 <c>Keycloak</c>。
/// </summary>
public sealed class KeycloakAdminOptions
{
    public const string SectionName = "Keycloak";

    /// <summary>浏览器使用的外部可达 realm 地址。</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>管理 API 基地址（可用容器内部地址）。</summary>
    public string AdminBaseUrl { get; set; } = string.Empty;

    /// <summary>realm 名称。</summary>
    public string Realm { get; set; } = "apromisedland";

    /// <summary>Blazor 登录客户端。</summary>
    public string WebClientId { get; set; } = "treegraph-web";

    /// <summary>Blazor 登录客户端密钥（由 Aspire 参数注入）。</summary>
    public string WebClientSecret { get; set; } = string.Empty;

    /// <summary>管理调用 service account 客户端。</summary>
    public string AdminClientId { get; set; } = "treegraph-admin";

    /// <summary>管理调用客户端密钥（由 Aspire 参数注入）。</summary>
    public string AdminClientSecret { get; set; } = string.Empty;

    /// <summary>是否要求 HTTPS 元数据。</summary>
    public bool RequireHttpsMetadata { get; set; }

    /// <summary>step-up 新鲜度阈值（秒）。</summary>
    public int StepUpMaxAgeSeconds { get; set; } = 300;

    /// <summary>空间组名前缀。</summary>
    public string SpaceGroupPrefix { get; set; } = "space-";

    /// <summary>Blazor 管理台外部基地址，用于生成 redirect URI。</summary>
    public string WebBaseUrl { get; set; } = "http://localhost:5783";

    /// <summary>纯 WASM 公共客户端。</summary>
    public string WasmClientId { get; set; } = "treegraph-wasm";

    /// <summary>纯 WASM 管理台外部基地址。</summary>
    public string WasmBaseUrl { get; set; } = "https://localhost:5787";

    /// <summary>MAUI 客户端回调地址。</summary>
    public string MauiRedirectUri { get; set; } = "treegraph://callback";

    /// <summary>期望的 Keycloak 版本（用于健康检查门禁）。</summary>
    public string ExpectedVersion { get; set; } = "26.6.3";

    /// <summary>审计库连接字符串名称。</summary>
    public string ConnectionStringName { get; set; } = "IdentityAdminDb";
}
