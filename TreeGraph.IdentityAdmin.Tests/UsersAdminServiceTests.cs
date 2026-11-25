using System.Net;
using TreeGraph.IdentityAdmin.Audit;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;
using TreeGraph.Shared.Identity.Admin;
using Xunit;

namespace TreeGraph.IdentityAdmin.Tests;

public class UsersAdminServiceTests
{
    private const string TokenJson = "{\"access_token\":\"tok\",\"expires_in\":300}";

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
            if (req.Method == HttpMethod.Put) return new HttpResponseMessage(HttpStatusCode.OK);
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
        var (svc, _, audit) = Build(req => req.Method == HttpMethod.Put
            ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.OK));

        var result = await svc.DisableAsync(TestDefaults.Actor(), "user-1");

        Assert.False(result.Success);
        Assert.Contains(audit.Completed, c => c.Result == "failed");
    }

    [Fact]
    public async Task DisableAsync_AuditUnavailable_FailClosed_NoKeycloakCall()
    {
        var auditDown = new FakeAdminAuditStore { ThrowOnBegin = true };
        var (svc, handler, _) = Build(
            _ => new HttpResponseMessage(HttpStatusCode.OK),
            audit: auditDown);

        var result = await svc.DisableAsync(TestDefaults.Actor(), "user-1");

        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);

        // 审计库不可用时不应触达 Keycloak
        Assert.DoesNotContain(handler.Requests,
            r => r.RequestUri?.ToString().Contains("/users", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task DisableAsync_LastActiveAdmin_Returns409WithoutPut()
    {
        var putCalls = 0;
        var (svc, _, _) = Build(req =>
        {
            if (req.Method == HttpMethod.Put)
            {
                putCalls++;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            if (req.RequestUri!.AbsolutePath.Contains("/roles/app-admin/users", StringComparison.Ordinal))
                return StubHttpMessageHandler.Json("""[{"id":"user-1","username":"admin","enabled":true}]""");

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var result = await svc.DisableAsync(TestDefaults.Actor(), "user-1");

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(0, putCalls);
    }

    [Fact]
    public async Task DisableAsync_MultipleActiveAdmins_AllowsDisable()
    {
        var (svc, _, _) = Build(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("/roles/app-admin/users", StringComparison.Ordinal))
                return StubHttpMessageHandler.Json(
                    """[{"id":"user-1","enabled":true},{"id":"user-2","enabled":true}]""");

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var result = await svc.DisableAsync(TestDefaults.Actor(), "user-1");

        Assert.True(result.Success);
    }
}
