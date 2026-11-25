namespace TreeGraph.Blazor.Configuration;

public sealed class WasmRuntimeConfig
{
    public KeycloakBrowserConfig Keycloak { get; init; } = new();
    public BrowserApiEndpoints Apis { get; init; } = new();

    public void Validate()
    {
        var authority = AbsoluteHttpsOrDevelopmentUri(Keycloak.Authority, "Keycloak:Authority");
        if (!authority.AbsolutePath.Contains("/realms/", StringComparison.Ordinal))
            throw new InvalidOperationException("Keycloak:Authority 必须包含 /realms/{realm}。");
        if (string.IsNullOrWhiteSpace(Keycloak.ClientId))
            throw new InvalidOperationException("缺少 Keycloak:ClientId。");

        _ = AbsoluteHttpsOrDevelopmentUri(Apis.TreeGraphApi, "Apis:TreeGraphApi");
        _ = AbsoluteHttpsOrDevelopmentUri(Apis.SyncServices, "Apis:SyncServices");
        _ = AbsoluteHttpsOrDevelopmentUri(Apis.FileStorageApi, "Apis:FileStorageApi");
        _ = AbsoluteHttpsOrDevelopmentUri(Apis.MafWfApi, "Apis:MafWfApi");
        _ = AbsoluteHttpsOrDevelopmentUri(Apis.IdentityAdminApi, "Apis:IdentityAdminApi");
    }

    private static Uri AbsoluteHttpsOrDevelopmentUri(string? value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps
                && !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
            throw new InvalidOperationException($"{name} 必须是 HTTPS 绝对地址；本机开发可使用 HTTP loopback。");
        return uri;
    }

    public static Uri ApiBaseAddress(string value)
        => new(value.EndsWith('/') ? value : value + "/", UriKind.Absolute);
}

public sealed class KeycloakBrowserConfig
{
    public string Authority { get; init; } = string.Empty;
    public string ClientId { get; init; } = "treegraph-wasm";
}

public sealed class BrowserApiEndpoints
{
    public string TreeGraphApi { get; init; } = string.Empty;
    public string SyncServices { get; init; } = string.Empty;
    public string FileStorageApi { get; init; } = string.Empty;
    public string MafWfApi { get; init; } = string.Empty;
    public string IdentityAdminApi { get; init; } = string.Empty;
}
