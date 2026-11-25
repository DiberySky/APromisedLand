using System.Net;
using System.Text.Json.Nodes;
using TreeGraph.IdentityAdmin.Modules;
using TreeGraph.Shared.Identity.Admin;
using Xunit;

namespace TreeGraph.IdentityAdmin.Tests;

public class RealmKeysAdminServiceTests
{
    private const string RealmPath = "/admin/realms/apromisedland";

    [Fact]
    public async Task ListAsync_MapsKeysAndValidTo()
    {
        var (client, runner, _) = TestDefaults.CreateServices(req =>
            req.RequestUri!.AbsolutePath == RealmPath + "/keys"
                ? StubHttpMessageHandler.Json(
                    """
                    [{"providerId":"k-1","kid":"kid-1","status":"ACTIVE","type":"RSA","algorithm":"RS256","providerPriority":100,"validTo":1760000000000}]
                    """)
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = new RealmKeysAdminService(client, runner);
        var keys = await service.ListAsync();

        var key = Assert.Single(keys);
        Assert.Equal("k-1", key.ProviderId);
        Assert.Equal("kid-1", key.Kid);
        Assert.True(key.IsActive);
        Assert.Equal("1760000000000", key.ValidTo);
    }

    [Fact]
    public async Task DeleteAsync_ActiveKey_RejectedWithoutDelete()
    {
        var deleteCalls = 0;
        var (client, runner, _) = TestDefaults.CreateServices(req =>
        {
            if (req.Method == HttpMethod.Delete)
                deleteCalls++;

            return req.RequestUri!.AbsolutePath == RealmPath + "/keys"
                ? StubHttpMessageHandler.Json("""[{"providerId":"k-1","status":"ACTIVE"}]""")
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var service = new RealmKeysAdminService(client, runner);
        var result = await service.DeleteAsync(TestDefaults.Actor(), "k-1");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(0, deleteCalls);
    }

    [Fact]
    public async Task DeleteAsync_PassiveKey_IssuesDelete()
    {
        var deleteCalls = 0;
        var (client, runner, audit) = TestDefaults.CreateServices(req =>
        {
            if (req.Method == HttpMethod.Delete)
            {
                deleteCalls++;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return req.RequestUri!.AbsolutePath == RealmPath + "/keys"
                ? StubHttpMessageHandler.Json("""[{"providerId":"k-1","status":"PASSIVE"}]""")
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new RealmKeysAdminService(client, runner);
        var result = await service.DeleteAsync(TestDefaults.Actor(), "k-1");

        Assert.True(result.Success);
        Assert.Equal(1, deleteCalls);
        Assert.Contains("realm-key.delete", audit.BeginActions);
    }

    [Fact]
    public async Task DeleteAsync_UnknownKey_Returns404()
    {
        var (client, runner, _) = TestDefaults.CreateServices(req =>
            req.RequestUri!.AbsolutePath == RealmPath + "/keys"
                ? StubHttpMessageHandler.Json("""[{"providerId":"other","status":"PASSIVE"}]""")
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = new RealmKeysAdminService(client, runner);
        var result = await service.DeleteAsync(TestDefaults.Actor(), "missing");

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task GenerateAsync_PostsComponentThenReturnsMappedKey()
    {
        var (client, runner, audit) = TestDefaults.CreateServices(req =>
        {
            var path = req.RequestUri!.AbsolutePath;

            if (req.Method == HttpMethod.Get && path == RealmPath)
                return StubHttpMessageHandler.Json("""{"id":"realm-internal","realm":"apromisedland"}""");

            if (req.Method == HttpMethod.Post && path == RealmPath + "/components")
                return TestDefaults.Created($"http://localhost:8323{RealmPath}/components/new-key");

            if (req.Method == HttpMethod.Get && path == RealmPath + "/keys")
                return StubHttpMessageHandler.Json(
                    """
                    [{"providerId":"new-key","kid":"kid-new","status":"ACTIVE","type":"RSA","algorithm":"RS256","providerPriority":100}]
                    """);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new RealmKeysAdminService(client, runner);
        var result = await service.GenerateAsync(TestDefaults.Actor(), new GenerateRealmKeyRequest());

        Assert.True(result.Success);
        Assert.Equal("new-key", result.Value!.ProviderId);
        Assert.Contains("realm-key.generate", audit.BeginActions);
    }
}

public class OrganizationsAdminServiceTests
{
    private const string RealmPath = "/admin/realms/apromisedland";

    [Fact]
    public async Task SearchAsync_MapsOrganizationsAndDomainCount()
    {
        var (client, runner, _) = TestDefaults.CreateServices(req =>
            req.RequestUri!.AbsolutePath == RealmPath + "/organizations"
                ? StubHttpMessageHandler.Json(
                    """
                    [{"id":"o1","name":"Acme","alias":"acme","enabled":true,"domains":["a.com","b.com"]}]
                    """)
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = new OrganizationsAdminService(client, runner);
        var page = await service.SearchAsync(new AdminPageRequest(Page: 1, PageSize: 10));

        var item = Assert.Single(page.Items);
        Assert.Equal("o1", item.Id);
        Assert.Equal("acme", item.Alias);
        Assert.True(item.Enabled);
        Assert.Equal(2, item.DomainCount);
    }

    [Fact]
    public async Task CreateAsync_PostsAndReturnsCreatedId()
    {
        var (client, runner, audit) = TestDefaults.CreateServices(req =>
            req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == RealmPath + "/organizations"
                ? TestDefaults.Created($"http://localhost:8323{RealmPath}/organizations/new-org")
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = new OrganizationsAdminService(client, runner);
        var result = await service.CreateAsync(
            TestDefaults.Actor(),
            new CreateOrganizationRequest("Acme", "acme", Domains: ["acme.com"]));

        Assert.True(result.Success);
        Assert.Equal("new-org", result.Value);
        Assert.Contains("organization.create", audit.BeginActions);
    }

    [Fact]
    public async Task AddMemberAsync_PostsToMembersEndpoint()
    {
        Uri? posted = null;
        var (client, runner, _) = TestDefaults.CreateServices(req =>
        {
            if (req.Method == HttpMethod.Post)
            {
                posted = req.RequestUri;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new OrganizationsAdminService(client, runner);
        var result = await service.AddMemberAsync(TestDefaults.Actor(), "o1", "user-1");

        Assert.True(result.Success);
        Assert.NotNull(posted);
        Assert.EndsWith("/organizations/o1/members", posted!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_MapsAttributesToFlatDictionary()
    {
        var (client, runner, _) = TestDefaults.CreateServices(req =>
            req.RequestUri!.AbsolutePath == RealmPath + "/organizations/o1"
                ? StubHttpMessageHandler.Json(
                    """
                    {"id":"o1","name":"Acme","alias":"acme","enabled":true,"attributes":{"tier":["gold"]}}
                    """)
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = new OrganizationsAdminService(client, runner);
        var detail = await service.GetAsync("o1");

        Assert.NotNull(detail);
        Assert.Equal("gold", detail!.Attributes["tier"]);
    }
}

public class ClientLogoutAdminServiceTests
{
    private const string RealmPath = "/admin/realms/apromisedland";

    [Fact]
    public async Task GetAsync_ReadsBackchannelAndFrontchannelAttributes()
    {
        var (client, runner, _) = TestDefaults.CreateServices(req =>
            req.RequestUri!.AbsolutePath == RealmPath + "/clients/my-client"
                ? StubHttpMessageHandler.Json(
                    """
                    {"clientId":"my-client","attributes":{
                      "backchannel.logout.session.required":"true",
                      "frontchannel.logout.url":"https://app/logout"
                    }}
                    """)
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = new ClientLogoutAdminService(client, runner);
        var settings = await service.GetAsync("my-client");

        Assert.NotNull(settings);
        Assert.True(settings!.BackchannelLogoutSessionRequired);
        Assert.False(settings.BackchannelLogoutRevokeOfflineTokens);
        Assert.Equal("https://app/logout", settings.FrontchannelLogoutUrl);
    }

    [Fact]
    public async Task UpdateAsync_PreservesExistingAttributes()
    {
        string? putBody = null;
        var (client, runner, audit) = TestDefaults.CreateServices(req =>
        {
            if (req.Method == HttpMethod.Get
                && (req.RequestUri!.AbsolutePath == RealmPath + "/clients/my-client"
                    || req.RequestUri.AbsolutePath == RealmPath + "/clients/uuid-my-client"))
                return StubHttpMessageHandler.Json(
                    """
                    {"id":"uuid-my-client","clientId":"my-client","attributes":{"some.other":"v","backchannel.logout.session.required":"false"}}
                    """);

            if (req.Method == HttpMethod.Put)
            {
                putBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = new ClientLogoutAdminService(client, runner);
        var result = await service.UpdateAsync(
            TestDefaults.Actor(),
            "my-client",
            new UpdateClientLogoutSettingsRequest(
                BackchannelLogoutSessionRequired: true,
                FrontchannelLogoutUrl: "https://app/logout"));

        Assert.True(result.Success);
        Assert.Contains("client.update-logout", audit.BeginActions);

        var attributes = JsonNode.Parse(putBody!)!["attributes"]!.AsObject();
        Assert.Equal("v", (string?)attributes["some.other"]);
        Assert.Equal("true", (string?)attributes["backchannel.logout.session.required"]);
        Assert.Equal("https://app/logout", (string?)attributes["frontchannel.logout.url"]);
    }

    [Fact]
    public async Task GetAsync_ClientMissing_ReturnsNull()
    {
        var (client, runner, _) = TestDefaults.CreateServices(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var service = new ClientLogoutAdminService(client, runner);
        var settings = await service.GetAsync("missing");

        Assert.Null(settings);
    }
}
