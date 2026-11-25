using System.Net;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;
using Xunit;

namespace TreeGraph.IdentityAdmin.Tests;

public class KeycloakAdminClientTests
{
    private const string TokenJson = "{\"access_token\":\"tok\",\"expires_in\":300}";

    private static (KeycloakAdminClient Client, StubHttpMessageHandler Handler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> adminResponder)
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("/token", StringComparison.Ordinal) == true)
                return StubHttpMessageHandler.Json(TokenJson);
            return adminResponder(req);
        });
        var factory = new FakeHttpClientFactory(handler);
        var options = new TestOptionsMonitor<KeycloakAdminOptions>(TestDefaults.Options());
        var tokenProvider = new AdminTokenProvider(
            factory, options, new TestTimeProvider(), TestDefaults.Logger<AdminTokenProvider>());
        var client = new KeycloakAdminClient(
            factory, tokenProvider, options, TestDefaults.Logger<KeycloakAdminClient>());
        return (client, handler);
    }

    private static int TokenRequests(StubHttpMessageHandler h)
        => h.Requests.Count(r => r.RequestUri?.ToString().Contains("/token", StringComparison.Ordinal) == true);

    private static int AdminRequests(StubHttpMessageHandler h)
        => h.Requests.Count(r => r.RequestUri?.ToString().Contains("/token", StringComparison.Ordinal) != true);

    [Fact]
    public async Task SendAsync_First401_RefreshesTokenAndRetriesOnce()
    {
        var calls = 0;
        var (client, handler) = Build(_ => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.OK));

        var response = await client.SendAsync(HttpMethod.Get, "users");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(2, AdminRequests(handler));
        Assert.Equal(2, TokenRequests(handler));
    }

    [Fact]
    public async Task SendAsync_Second401AfterRefresh_ThrowsUnauthorized()
    {
        var (client, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<KeycloakUnauthorizedException>(
            () => client.SendAsync(HttpMethod.Get, "users"));
    }

    [Fact]
    public void BuildQuery_SkipsNullOrEmptyAndEncodes()
    {
        var query = KeycloakAdminClient.BuildQuery(
            ("search", "a b"),
            ("enabled", null),
            ("empty", ""),
            ("max", "10"));

        Assert.Equal("?search=a%20b&max=10", query);
    }

    [Fact]
    public void BuildQuery_NoParams_ReturnsEmpty()
        => Assert.Equal(string.Empty, KeycloakAdminClient.BuildQuery());
}
