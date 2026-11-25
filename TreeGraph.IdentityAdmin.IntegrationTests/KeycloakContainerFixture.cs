using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;
using Xunit;

namespace TreeGraph.IdentityAdmin.IntegrationTests;

/// <summary>
/// 启动一次性 Keycloak 容器（默认 26.8.2，可用 KEYCLOAK_TEST_IMAGE 覆盖），
/// 并创建独立的 it-realm + service account 客户端供管理 API 测试使用。
/// </summary>
public sealed class KeycloakContainerFixture : IAsyncLifetime
{
    private const int HttpPort = 8080;
    private const string TestRealm = "it-realm";
    private const string TestClientId = "it-admin";
    private const string TestClientSecret = "it-admin-secret";

    private readonly HttpClient _http = new();
    private IContainer? _container;

    public string Image { get; } =
        Environment.GetEnvironmentVariable("KEYCLOAK_TEST_IMAGE") ?? "quay.io/keycloak/keycloak:26.8.2";

    public string BaseUrl { get; private set; } = string.Empty;

    public string Version { get; private set; } = string.Empty;

    public KeycloakAdminClient Client { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder()
            .WithImage(Image)
            .WithPortBinding(HttpPort, true)
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
            .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", "admin")
            .WithCommand("start-dev", "--features=organization,admin-api")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/realms/master")))
            .Build();

        await _container.StartAsync();
        BaseUrl = $"http://{_container.Hostname}:{_container.GetMappedPublicPort(HttpPort)}".Replace("://localhost:", "://127.0.0.1:");

        var masterToken = await GetMasterTokenAsync();
        await EnsureRealmAsync(masterToken);
        var adminClientUuid = await EnsureAdminClientAsync(masterToken);
        await AssignRealmManagementRolesAsync(masterToken, adminClientUuid);

        var options = new KeycloakAdminOptions
        {
            AdminBaseUrl = BaseUrl,
            Realm = TestRealm,
            AdminClientId = TestClientId,
            AdminClientSecret = TestClientSecret
        };

        var monitor = new StaticOptionsMonitor<KeycloakAdminOptions>(options);
        var factory = new SimpleHttpClientFactory();
        var tokenProvider = new AdminTokenProvider(
            factory, monitor, TimeProvider.System, NullLogger<AdminTokenProvider>.Instance);

        Client = new KeycloakAdminClient(
            factory, tokenProvider, monitor, NullLogger<KeycloakAdminClient>.Instance);

        Version = await ReadVersionAsync(masterToken);
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    private async Task<string> GetMasterTokenAsync()
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "admin",
            ["password"] = "admin"
        });

        using var response = await _http.PostAsync(
            $"{BaseUrl}/realms/master/protocol/openid-connect/token", content);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("access_token").GetString()!;
    }

    private async Task EnsureRealmAsync(string token)
    {
        using var response = await SendJsonAsync(HttpMethod.Post, $"{BaseUrl}/admin/realms", token,
            new { realm = TestRealm, enabled = true, organizationsEnabled = true });

        // 201 表示创建成功；409 表示已存在（幂等）。
        if (response.StatusCode is not System.Net.HttpStatusCode.Created
            and not System.Net.HttpStatusCode.Conflict)
            response.EnsureSuccessStatusCode();
    }

    private async Task<string> EnsureAdminClientAsync(string token)
    {
        using var created = await SendJsonAsync(HttpMethod.Post, $"{BaseUrl}/admin/realms/{TestRealm}/clients", token,
            new
            {
                clientId = TestClientId,
                enabled = true,
                protocol = "openid-connect",
                publicClient = false,
                clientAuthenticatorType = "client-secret",
                secret = TestClientSecret,
                standardFlowEnabled = false,
                directAccessGrantsEnabled = false,
                serviceAccountsEnabled = true
            });

        using var list = await GetJsonAsync($"{BaseUrl}/admin/realms/{TestRealm}/clients?clientId={TestClientId}", token);
        return list.RootElement[0].GetProperty("id").GetString()!;
    }

    private async Task AssignRealmManagementRolesAsync(string token, string adminClientUuid)
    {
        using var clients = await GetJsonAsync(
            $"{BaseUrl}/admin/realms/{TestRealm}/clients?clientId=realm-management", token);
        var realmManagementUuid = clients.RootElement[0].GetProperty("id").GetString()!;

        using var roles = await GetJsonAsync(
            $"{BaseUrl}/admin/realms/{TestRealm}/clients/{realmManagementUuid}/roles", token);

        var desired = new List<object>();
        foreach (var role in roles.RootElement.EnumerateArray())
            desired.Add(new { id = role.GetProperty("id").GetString(), name = role.GetProperty("name").GetString() });

        using var serviceAccount = await GetJsonAsync(
            $"{BaseUrl}/admin/realms/{TestRealm}/clients/{adminClientUuid}/service-account-user", token);
        var userId = serviceAccount.RootElement.GetProperty("id").GetString();

        using var response = await SendJsonAsync(
            HttpMethod.Post,
            $"{BaseUrl}/admin/realms/{TestRealm}/users/{userId}/role-mappings/clients/{realmManagementUuid}",
            token,
            desired);
        response.EnsureSuccessStatusCode();
    }

    private async Task<string> ReadVersionAsync(string token)
    {
        using var info = await GetJsonAsync($"{BaseUrl}/admin/serverinfo", token);
        return info.RootElement.GetProperty("systemInfo").GetProperty("version").GetString() ?? string.Empty;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method, string url, string token, object body)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _http.SendAsync(request);
    }
}

internal sealed class SimpleHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _client = new();

    public HttpClient CreateClient(string name) => _client;
}

internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StaticOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

[CollectionDefinition("keycloak")]
public sealed class KeycloakCollection : ICollectionFixture<KeycloakContainerFixture>;
