using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;
using Xunit;

namespace TreeGraph.IdentityAdmin.Api.Tests;

public sealed class AdminUsersApiTests : IDisposable
{
    private readonly AdminApiFactory _factory = new();

    [Fact]
    public async Task Users_Anonymous_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_NonAdmin_Returns403()
    {
        var response = await SendAsync("/api/admin/users", role: "reader");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, _factory.Users.SearchCount);
    }

    [Fact]
    public async Task Users_Admin_ReturnsResults()
    {
        var response = await SendAsync("/api/admin/users?search=alice&page=2&pageSize=10", role: IdentityRoles.Admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<IdentityUserSummary>>();
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.Equal("alice", result.Items[0].Username);
        Assert.Equal(2, _factory.Users.LastSearch?.Page);
    }

    [Fact]
    public async Task Users_Superuser_ReturnsResults()
    {
        var response = await SendAsync("/api/admin/users", role: IdentityRoles.Superuser);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _factory.Users.SearchCount);
    }

    [Fact]
    public async Task Disable_StaleAuthentication_Returns428WithoutCallingService()
    {
        var response = await SendAsync(
            "/api/admin/users/user-1/disable",
            HttpMethod.Post,
            IdentityRoles.Admin,
            DateTimeOffset.UtcNow.AddMinutes(-10));
        Assert.Equal((HttpStatusCode)428, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("step_up_required", body, StringComparison.Ordinal);
        Assert.Equal(0, _factory.Users.DisableCount);
    }

    [Fact]
    public async Task Disable_FreshAdmin_UsesServerSideActor()
    {
        var response = await SendAsync("/api/admin/users/user-1/disable", HttpMethod.Post, IdentityRoles.Admin);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _factory.Users.DisableCount);
        Assert.Equal("test-subject", _factory.Users.LastActor?.Sub);
        Assert.Equal("test-admin", _factory.Users.LastActor?.Username);
        Assert.Equal("user-1", _factory.Users.LastUserId);
    }

    [Fact]
    public async Task HighRiskWrites_StaleAuthentication_AllReturn428()
    {
        var stale = DateTimeOffset.UtcNow.AddMinutes(-10);
        var requests = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Put, "/api/admin/user-federation/component-1", new SaveLdapFederationRequest(
                "ldap", true, true, false, false, "ad", "ldap://localhost:389", "ou=users,dc=example,dc=com",
                null, null, null, null, null, null, null)),
            (HttpMethod.Delete, "/api/admin/user-federation/component-1", null),
            (HttpMethod.Post, "/api/admin/clients/client-1/secret/regenerate", null),
            (HttpMethod.Post, "/api/admin/realm-keys", new GenerateRealmKeyRequest()),
            (HttpMethod.Delete, "/api/admin/realm-keys/provider-1", null),
            (HttpMethod.Post, "/api/admin/users/user-1/reset-password", new ResetPasswordRequest("secret", true)),
            (HttpMethod.Post, "/api/admin/users/user-1/disable", null),
            (HttpMethod.Put, "/api/admin/realm/smtp", new SmtpSettingsRequest("smtp.local", 25)),
            (HttpMethod.Put, "/api/admin/auth-flow-bindings/browser", new { FlowAlias = "browser" }),
            (HttpMethod.Delete, "/api/admin/groups/group-1", null)
        };

        foreach (var item in requests)
        {
            var response = await SendAsync(item.Url, item.Method, IdentityRoles.Admin, stale, item.Body);
            Assert.True(
                response.StatusCode == (HttpStatusCode)428,
                $"{item.Method} {item.Url} 应返回 428，实际为 {(int)response.StatusCode}：{await response.Content.ReadAsStringAsync()}");
        }
    }

    private Task<HttpResponseMessage> SendAsync(
        string url,
        HttpMethod? method = null,
        string role = IdentityRoles.Admin,
        DateTimeOffset? authTime = null,
        object? body = null)
    {
        var request = new HttpRequestMessage(method ?? HttpMethod.Get, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, body.GetType());
        request.Headers.Add("X-Test-Auth", "true");
        request.Headers.Add("X-Test-Role", role);
        request.Headers.Add("X-Test-Auth-Time", (authTime ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds().ToString());
        return _factory.CreateClient().SendAsync(request);
    }

    public void Dispose() => _factory.Dispose();
}

public sealed class AdminApiFactory : WebApplicationFactory<Program>
{
    public FakeUsersAdminService Users { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("IdentityAdmin:EnableBootstrap", "false");
        builder.UseSetting("IdentityAdmin:EnableDatabaseMigration", "false");
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "TreeGraph.IdentityAdmin.Api.Tests"));
        builder.UseSetting("Keycloak:AdminBaseUrl", "https://localhost:8323");
        builder.UseSetting("Keycloak:Authority", "https://localhost:8323/realms/apromisedland");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IUsersAdminService>(Users);
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        });
    }
}

public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-Auth"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(IdentityClaimTypes.Subject, "test-subject"),
            new(IdentityClaimTypes.PreferredUsername, "test-admin"),
            new(IdentityClaimTypes.Roles, Request.Headers["X-Test-Role"].ToString()),
            new(IdentityClaimTypes.AuthTime, Request.Headers["X-Test-Auth-Time"].ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test", IdentityClaimTypes.PreferredUsername, IdentityClaimTypes.Roles);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public sealed class FakeUsersAdminService : IUsersAdminService
{
    public int SearchCount { get; private set; }
    public int DisableCount { get; private set; }
    public AdminPageRequest? LastSearch { get; private set; }
    public AdminActor? LastActor { get; private set; }
    public string? LastUserId { get; private set; }

    public Task<PagedResult<IdentityUserSummary>> SearchAsync(AdminPageRequest request, CancellationToken ct = default)
    {
        SearchCount++;
        LastSearch = request;
        return Task.FromResult(new PagedResult<IdentityUserSummary>(
            [new IdentityUserSummary("user-1", "alice", null, null, true, null)], 1, request.Page, request.PageSize));
    }

    public Task<IdentityUserDetail?> GetAsync(string userId, CancellationToken ct = default)
        => Task.FromResult<IdentityUserDetail?>(new IdentityUserDetail(
            userId, "alice", null, null, true, null, [], [], [],
            new Dictionary<string, IReadOnlyList<string>>()));

    public Task<AdminResult<DisableUserResult>> DisableAsync(AdminActor actor, string userId, CancellationToken ct = default)
    {
        DisableCount++;
        LastActor = actor;
        LastUserId = userId;
        return Task.FromResult(AdminResult<DisableUserResult>.Ok(new DisableUserResult(true, true, true, false)));
    }

    public Task<AdminResult> EnableAsync(AdminActor actor, string userId, CancellationToken ct = default)
        => Task.FromResult(AdminResult.Ok());

    public Task<AdminResult<string>> CreateAsync(AdminActor actor, CreateUserRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();
    public Task<AdminResult> UpdateAsync(AdminActor actor, string userId, UpdateUserRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();
    public Task<AdminResult> ResetPasswordAsync(AdminActor actor, string userId, ResetPasswordRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();
    public Task<AdminResult> LogoutSessionsAsync(AdminActor actor, string userId, CancellationToken ct = default)
        => throw new NotSupportedException();
}
