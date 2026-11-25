using System.Net;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Backup;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Modules;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;
using TreeGraph.Shared.Identity.Admin;
using Xunit;

namespace TreeGraph.IdentityAdmin.Tests;

public class AuthFlowsAdminServiceTests
{
    private const string TokenJson = "{\"access_token\":\"tok\",\"expires_in\":300}";

    private sealed class RecordingExportAdminService : IExportAdminService
    {
        public int CallCount { get; private set; }
        public bool Throw { get; set; }
        public bool Fail { get; set; }

        public Task<AdminResult<RealmExportResult>> ExportAsync(
            AdminActor actor, RealmExportOptions options, CancellationToken ct = default)
        {
            CallCount++;
            if (Throw) throw new InvalidOperationException("backup unavailable");
            var result = Fail
                ? new AdminResult<RealmExportResult>(false, null, "backup failed", 502)
                : new AdminResult<RealmExportResult>(true, new RealmExportResult("{}", "backup.json", DateTimeOffset.UtcNow));
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingBackupStore : IRealmBackupStore
    {
        public int SaveCount { get; private set; }
        public bool Throw { get; set; }

        public Task<Guid> SaveAsync(
            string realm, string fileName, string payloadJson, string actorSub, string action,
            CancellationToken ct = default)
        {
            if (Throw) throw new InvalidOperationException("backup store unavailable");
            SaveCount++;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<IReadOnlyList<RealmBackupRecord>> ListAsync(int take = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RealmBackupRecord>>([]);

        public Task<(RealmBackupRecord Record, string PayloadJson)?> GetLatestAsync(CancellationToken ct = default)
            => Task.FromResult<(RealmBackupRecord Record, string PayloadJson)?>(null);
    }

    private static (AuthFlowsAdminService Service, StubHttpMessageHandler Handler,
        RecordingExportAdminService Export, RecordingBackupStore Backup) Build(
        Func<HttpRequestMessage, HttpResponseMessage> adminResponder,
        RecordingExportAdminService? export = null,
        RecordingBackupStore? backup = null)
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
        var audit = new FakeAdminAuditStore();
        var runner = new AdminOperationRunner(audit, options, TestDefaults.Logger<AdminOperationRunner>());
        export ??= new RecordingExportAdminService();
        backup ??= new RecordingBackupStore();
        var svc = new AuthFlowsAdminService(
            client, runner, export, backup, TestDefaults.Logger<AuthFlowsAdminService>());
        return (svc, handler, export, backup);
    }

    private static HttpResponseMessage RealmOk(HttpRequestMessage req)
    {
        if (req.Method == HttpMethod.Get)
            return StubHttpMessageHandler.Json("{\"browserFlow\":\"browser\"}");
        if (req.Method == HttpMethod.Put)
            return new HttpResponseMessage(HttpStatusCode.OK);
        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateBindingAsync_ValidBinding_BackupPersistedThenPut()
    {
        var (svc, handler, export, backup) = Build(RealmOk);

        var result = await svc.UpdateBindingAsync(TestDefaults.Actor(), "browserFlow", "my-browser");

        Assert.True(result.Success);
        Assert.Equal(1, export.CallCount);
        Assert.Equal(1, backup.SaveCount);
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task UpdateBindingAsync_InvalidBindingType_NoBackupAndReturns400()
    {
        var (svc, _, export, backup) = Build(RealmOk);

        var result = await svc.UpdateBindingAsync(TestDefaults.Actor(), "not-a-real-binding", "x");

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, export.CallCount);
        Assert.Equal(0, backup.SaveCount);
    }

    [Fact]
    public async Task UpdateBindingAsync_BackupThrows_RejectsWriteAndNoKeycloakCall()
    {
        var export = new RecordingExportAdminService { Throw = true };
        var (svc, handler, _, _) = Build(RealmOk, export);

        var result = await svc.UpdateBindingAsync(TestDefaults.Actor(), "browserFlow", "my-browser");

        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(1, export.CallCount);
        // 备份失败（fail-closed）：不应触达 realm 写入
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task UpdateBindingAsync_BackupReturnsFailure_RejectsWrite()
    {
        var export = new RecordingExportAdminService { Fail = true };
        var (svc, handler, _, _) = Build(RealmOk, export);

        var result = await svc.UpdateBindingAsync(TestDefaults.Actor(), "browserFlow", "my-browser");

        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(1, export.CallCount);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task UpdateBindingAsync_BackupStoreThrows_RejectsWrite()
    {
        var backup = new RecordingBackupStore { Throw = true };
        var (svc, handler, _, _) = Build(RealmOk, backup: backup);

        var result = await svc.UpdateBindingAsync(TestDefaults.Actor(), "browserFlow", "my-browser");

        // 导出成功但加密持久化失败：同样拒绝写入
        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task DeleteAsync_BackupFails_RejectsWriteAndNoDeleteCall()
    {
        var export = new RecordingExportAdminService { Fail = true };
        var (svc, handler, _, _) = Build(RealmOk, export);

        var result = await svc.DeleteAsync(TestDefaults.Actor(), "some-flow");

        Assert.False(result.Success);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(1, export.CallCount);
        Assert.DoesNotContain(handler.Requests, r => r.Method == HttpMethod.Delete);
    }
}
