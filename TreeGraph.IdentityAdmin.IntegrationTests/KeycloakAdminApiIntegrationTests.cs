using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace TreeGraph.IdentityAdmin.IntegrationTests;

[Collection("keycloak")]
public class KeycloakAdminApiIntegrationTests
{
    private readonly KeycloakContainerFixture _fixture;

    public KeycloakAdminApiIntegrationTests(KeycloakContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public void ContainerReportsVersion()
    {
        Assert.False(string.IsNullOrWhiteSpace(_fixture.Version));
        Assert.StartsWith("26.", _fixture.Version);
    }

    [Fact]
    public async Task Realm_IsReadableWithServiceAccountToken()
    {
        var response = await _fixture.Client.GetAsync(string.Empty);

        Assert.True(response.IsSuccess);
        Assert.Contains("it-realm", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClientScopes_IncludeAdminApiScopeOnSupportedVersions()
    {
        var response = await _fixture.Client.GetAsync("client-scopes");

        Assert.True(response.IsSuccess, $"HTTP {response.StatusCode}: {response.Body}");

        // kc-admin-api 由 26.8.x 提供；26.6 仅验证端点可用。
        if (_fixture.Version.StartsWith("26.8", StringComparison.Ordinal))
            Assert.Contains("kc-admin-api", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RealmKeys_ContainActiveSigningKey()
    {
        var response = await _fixture.Client.GetAsync("keys");

        Assert.True(response.IsSuccess);
        Assert.Contains("RS256", response.Body, StringComparison.Ordinal);
        Assert.Contains("ACTIVE", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task User_Crud_RoundTrip()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"it-user-{suffix}";

        var created = await _fixture.Client.SendAsync(HttpMethod.Post, "users", new
        {
            username,
            email = $"{username}@example.com",
            enabled = true,
            emailVerified = false
        });
        Assert.True(created.IsSuccess);

        var search = await _fixture.Client.GetAsync($"users?username={username}&exact=true");
        Assert.True(search.IsSuccess);
        using var document = JsonDocument.Parse(search.Body);
        var user = document.RootElement.EnumerateArray().Single();
        var userId = user.GetProperty("id").GetString()!;

        var updated = await _fixture.Client.SendAsync(HttpMethod.Put, $"users/{userId}", new
        {
            firstName = "Integration",
            lastName = "Test",
            email = $"{username}@example.com",
            enabled = true
        });
        Assert.True(updated.IsSuccess);

        var disabled = await _fixture.Client.SendAsync(HttpMethod.Put, $"users/{userId}", new { enabled = false });
        Assert.True(disabled.IsSuccess);

        var deleted = await _fixture.Client.SendAsync(HttpMethod.Delete, $"users/{userId}");
        Assert.True(deleted.IsSuccess);

        var missing = await _fixture.Client.GetAsync($"users/{userId}");
        Assert.Equal((int)HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Organizations_EndpointAvailable()
    {
        // Keycloak 26 才有 Organizations；26.6 / 26.8.2 均应返回 200。
        var response = await _fixture.Client.GetAsync("organizations?first=0&max=10");

        Assert.True(response.IsSuccess, $"HTTP {response.StatusCode}: {response.Body}");
    }

    [Fact]
    public async Task ClientLogoutAttributes_RoundTrip()
    {
        var clientId = $"it-logout-{Guid.NewGuid():N}"[..20];

        var created = await _fixture.Client.SendAsync(HttpMethod.Post, "clients", new
        {
            clientId,
            enabled = true,
            protocol = "openid-connect",
            publicClient = true,
            standardFlowEnabled = true,
            directAccessGrantsEnabled = false
        });
        Assert.True(created.IsSuccess, $"create client: HTTP {created.StatusCode}: {created.Body}");

        var list = await _fixture.Client.GetAsync($"clients?clientId={clientId}&exact=true");
        Assert.True(list.IsSuccess, $"list client: HTTP {list.StatusCode}: {list.Body}");

        var uuid = JsonDocument.Parse(list.Body).RootElement[0].GetProperty("id").GetString()!;

        var updated = await _fixture.Client.SendAsync(HttpMethod.Put, $"clients/{uuid}", new
        {
            id = uuid,
            clientId,
            enabled = true,
            protocol = "openid-connect",
            publicClient = true,
            standardFlowEnabled = true,
            attributes = new Dictionary<string, string>
            {
                ["backchannel.logout.session.required"] = "true",
                ["frontchannel.logout.url"] = "https://example.com/logout"
            }
        });
        Assert.True(updated.IsSuccess, $"put client: HTTP {updated.StatusCode}: {updated.Body}");

        var refreshed = await _fixture.Client.GetAsync($"clients/{uuid}");
        Assert.True(refreshed.IsSuccess, $"get client: HTTP {refreshed.StatusCode}: {refreshed.Body}");
        using var document = JsonDocument.Parse(refreshed.Body);
        var attributes = document.RootElement.GetProperty("attributes");
        Assert.Equal("true", attributes.GetProperty("backchannel.logout.session.required").GetString());
        Assert.Equal("https://example.com/logout", attributes.GetProperty("frontchannel.logout.url").GetString());

        await _fixture.Client.SendAsync(HttpMethod.Delete, $"clients/{uuid}");
    }
}
