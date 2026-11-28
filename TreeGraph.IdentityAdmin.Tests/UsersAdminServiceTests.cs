using System.Net;
using System.Text.Json;
using TreeGraph.IdentityAdmin.Audit;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;
using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;
using Xunit;

namespace TreeGraph.IdentityAdmin.Tests;

public class UsersAdminServiceTests
{
    private const string TokenJson = "{\"access_token\":\"tok\",\"expires_in\":300}";
    private const string RealmPath = "/admin/realms/apromisedland";

    private static (UsersAdminService Service, StubHttpMessageHandler Handler, FakeAdminAuditStore Audit) Build(
        Func<HttpRequestMessage, HttpResponseMessage> adminResponder,
        IIdentityRevocationStore? revocation = null,
        FakeAdminAuditStore? audit = null)
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

        audit ??= new FakeAdminAuditStore();
        var runner = new AdminOperationRunner(audit, options, TestDefaults.Logger<AdminOperationRunner>());
        var svc = new UsersAdminService(client, runner, revocation ?? new FakeRevocationStore());
        return (svc, handler, audit);
    }

    [Fact]
    public async Task DisableAsync_LogoutFails_MarksPartialAndRetryable()
    {
        var (svc, _, audit) = Build(req =>
        {
            if (TryGetNormalTarget(req, out var target)) return target;
            if (req.Method == HttpMethod.Put) return new HttpResponseMessage(HttpStatusCode.NoContent);
            if (req.Method == HttpMethod.Post) return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await svc.DisableAsync(TestDefaults.Actor(), "user-1");

        Assert.True(result.Success);
        var value = result.Value!;
        Assert.True(value.Disabled);
        Assert.False(value.SessionsRevoked);
        Assert.True(value.RevocationMarked);
        Assert.True(value.Retryable);
        Assert.Contains("user.disable", audit.BeginActions);
        Assert.Contains(audit.Completed, c => c.Result == "partial");
    }

    [Fact]
    public async Task DisableAsync_DisableFails_ReturnsFailure()
    {
        var (svc, _, audit) = Build(req =>
        {
            if (TryGetNormalTarget(req, out var target)) return target;
            return req.Method == HttpMethod.Put
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var result = await svc.DisableAsync(TestDefaults.Actor(), "user-1");

        Assert.False(result.Success);
        Assert.Contains(audit.Completed, c => c.Result == "failed");
    }

    [Fact]
    public async Task DisableAsync_AuditUnavailable_FailClosed_NoKeycloakWrite()
    {
        var auditDown = new FakeAdminAuditStore { ThrowOnBegin = true };
        var (svc, handler, _) = Build(
            req => TryGetNormalTarget(req, out var target)
                ? target
                : new HttpResponseMessage(HttpStatusCode.NoContent),
            audit: auditDown);

        var result = await svc.DisableAsync(TestDefaults.Actor(), "user-1");

        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.DoesNotContain(handler.Requests,
            r => r.Method != HttpMethod.Get
                 && r.RequestUri?.AbsolutePath.Contains("/users", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task DisableAsync_AdminTargetsAdmin_Returns403WithoutWrite()
    {
        var putCalls = 0;
        var (svc, _, _) = Build(req =>
        {
            if (TryGetTarget(req, "admin-1", "admin-2", [IdentityRoles.Admin], out var target)) return target;
            if (req.Method == HttpMethod.Put) putCalls++;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var result = await svc.DisableAsync(TestDefaults.Actor(), "admin-1");

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(0, putCalls);
    }

    [Fact]
    public async Task DisableAsync_SuperuserTarget_AlwaysReturns409()
    {
        var putCalls = 0;
        var (svc, _, _) = Build(req =>
        {
            if (TryGetTarget(
                    req, IdentityConstants.SuperuserId, IdentityConstants.SuperuserUsername,
                    [IdentityRoles.Superuser, IdentityRoles.Admin], out var target)) return target;
            if (req.Method == HttpMethod.Put) putCalls++;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var actor = new AdminActor(
            IdentityConstants.SuperuserId,
            IdentityConstants.SuperuserUsername,
            [IdentityRoles.Superuser, IdentityRoles.Admin]);

        var result = await svc.DisableAsync(actor, IdentityConstants.SuperuserId);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(0, putCalls);
    }

    [Fact]
    public async Task CreateAsync_AdminCreatesAdmin_Returns403WithoutKeycloakCall()
    {
        var (svc, handler, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        var result = await svc.CreateAsync(
            TestDefaults.Actor(),
            new CreateUserRequest("new-admin", "新管理员", "UnitTest1!", [IdentityRoles.Admin]));

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CreateAsync_NormalUserRequiresMainlandPhone()
    {
        var (svc, _, _) = Build(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        var result = await svc.CreateAsync(
            TestDefaults.Actor(),
            new CreateUserRequest("ordinary-name", "普通用户", "UnitTest1!"));

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_NormalUser_UsesPhoneAndRealNameWithoutEmail()
    {
        string? createBody = null;
        var (svc, _, _) = Build(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Post && path == RealmPath + "/users")
            {
                createBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return TestDefaults.Created("https://localhost/admin/realms/apromisedland/users/user-new");
            }
            if (req.Method == HttpMethod.Get && path == RealmPath + "/roles/" + IdentityRoles.Viewer)
                return StubHttpMessageHandler.Json("""{"id":"role-viewer","name":"app-viewer"}""");
            if (req.Method == HttpMethod.Post && path == RealmPath + "/users/user-new/role-mappings/realm")
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await svc.CreateAsync(
            TestDefaults.Actor(),
            new CreateUserRequest("13908639757", "测试用户", "UnitTest1!"));

        Assert.True(result.Success, result.Error);
        Assert.Equal("user-new", result.Value);
        Assert.NotNull(createBody);
        using var json = JsonDocument.Parse(createBody);
        var root = json.RootElement;
        Assert.Equal("13908639757", root.GetProperty("username").GetString());
        Assert.Equal("测试用户", root.GetProperty("attributes").GetProperty("realName")[0].GetString());
        Assert.Equal("13908639757", root.GetProperty("attributes").GetProperty("phoneNumber")[0].GetString());
        Assert.True(root.GetProperty("credentials")[0].GetProperty("temporary").GetBoolean());
        Assert.False(root.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task ResetPasswordAsync_ResetsLogsOutAndRevokesOldTokens()
    {
        var revocation = new FakeRevocationStore();
        var (svc, handler, _) = Build(req =>
        {
            if (TryGetNormalTarget(req, out var target)) return target;
            if (req.Method == HttpMethod.Put
                && req.RequestUri!.AbsolutePath == RealmPath + "/users/user-1/reset-password")
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            if (req.Method == HttpMethod.Post
                && req.RequestUri!.AbsolutePath == RealmPath + "/users/user-1/logout")
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }, revocation);

        var result = await svc.ResetPasswordAsync(
            TestDefaults.Actor(), "user-1", new ResetPasswordRequest("NewUnitTest1!", true));

        Assert.True(result.Success, result.Error);
        Assert.Contains("user-1", revocation.Revoked);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath.EndsWith("/logout", StringComparison.Ordinal));
    }

    private static bool TryGetNormalTarget(HttpRequestMessage request, out HttpResponseMessage response)
        => TryGetTarget(request, "user-1", "13908639757", [IdentityRoles.Viewer], out response);

    private static bool TryGetTarget(
        HttpRequestMessage request,
        string userId,
        string username,
        string[] roles,
        out HttpResponseMessage response)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get && path == RealmPath + "/users/" + userId)
        {
            response = StubHttpMessageHandler.Json(
                $"{{\"id\":\"{userId}\",\"username\":\"{username}\",\"enabled\":true,\"attributes\":{{\"realName\":[\"测试用户\"]}}}}");
            return true;
        }
        if (request.Method == HttpMethod.Get && path == RealmPath + "/users/" + userId + "/role-mappings/realm")
        {
            var roleJson = string.Join(',', roles.Select((r, i) => $"{{\"id\":\"role-{i}\",\"name\":\"{r}\"}}"));
            response = StubHttpMessageHandler.Json("[" + roleJson + "]");
            return true;
        }

        response = null!;
        return false;
    }
}

public class RolesAdminServiceTests
{
    [Fact]
    public async Task AddMemberAsync_AdminCannotGrantAdmin()
    {
        var (client, runner, _) = TestDefaults.CreateServices(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/users/user-1", StringComparison.Ordinal))
                return StubHttpMessageHandler.Json("""{"id":"user-1","username":"13908639757","enabled":true}""");
            if (path.EndsWith("/users/user-1/role-mappings/realm", StringComparison.Ordinal))
                return StubHttpMessageHandler.Json("""[{"id":"viewer","name":"app-viewer"}]""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var service = new RolesAdminService(client, runner);

        var result = await service.AddMemberAsync(TestDefaults.Actor(), IdentityRoles.Admin, "user-1");

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task AddMemberAsync_SuperuserRoleCannotBeGranted()
    {
        var (client, runner, _) = TestDefaults.CreateServices(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = new RolesAdminService(client, runner);
        var actor = new AdminActor("super-id", IdentityConstants.SuperuserUsername, [IdentityRoles.Superuser]);

        var result = await service.AddMemberAsync(actor, IdentityRoles.Superuser, "other-user");

        Assert.False(result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task RemoveMemberAsync_SuperuserCannotDowngradeSelf()
    {
        var (client, runner, _) = TestDefaults.CreateServices(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/users/super-id", StringComparison.Ordinal))
                return StubHttpMessageHandler.Json(
                    $"{{\"id\":\"super-id\",\"username\":\"{IdentityConstants.SuperuserUsername}\",\"enabled\":true}}");
            if (path.EndsWith("/users/super-id/role-mappings/realm", StringComparison.Ordinal))
                return StubHttpMessageHandler.Json(
                    """[{"id":"super","name":"app-superuser"},{"id":"admin","name":"app-admin"}]""");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var service = new RolesAdminService(client, runner);
        var actor = new AdminActor(
            "super-id", IdentityConstants.SuperuserUsername, [IdentityRoles.Superuser, IdentityRoles.Admin]);

        var result = await service.RemoveMemberAsync(actor, IdentityRoles.Admin, "super-id");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
    }
}
