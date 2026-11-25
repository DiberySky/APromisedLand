using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TreeGraph.IdentityAdmin.Audit;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Tests;

/// <summary>记录所有请求并按委托返回响应的 HttpMessageHandler。</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();
    public TimeSpan Delay { get; set; }

    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => _responder = responder;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (Delay > TimeSpan.Zero)
            await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
        return _responder(request);
    }

    public static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
}

/// <summary>始终返回同一 handler 的 IHttpClientFactory。</summary>
public sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public List<string> CreatedNames { get; } = new();

    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name)
    {
        CreatedNames.Add(name);
        return new HttpClient(_handler);
    }
}

/// <summary>可手动推进时间的 TimeProvider。</summary>
public sealed class TestTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => UtcNow;
    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
}

/// <summary>固定值的 IOptionsMonitor。</summary>
public sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value) => CurrentValue = value;
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

/// <summary>内存审计存储，记录 begin/complete，可配置 fail-closed。</summary>
public sealed class FakeAdminAuditStore : IAdminAuditStore
{
    public List<string> BeginActions { get; } = new();
    public List<(Guid Id, string Result)> Completed { get; } = new();
    public List<string> Fallback { get; } = new();
    public bool ThrowOnBegin { get; set; }

    public Task<Guid> BeginAsync(
        string actorSub, string? actorName, string? serviceClientId, string action,
        string? targetType, string? targetId, string? requestJson, string correlationId,
        CancellationToken ct = default)
    {
        if (ThrowOnBegin)
            throw new InvalidOperationException("audit store unavailable");
        BeginActions.Add(action);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task CompleteAsync(Guid id, string result, int? keycloakStatus, string? error, CancellationToken ct = default)
    {
        Completed.Add((id, result));
        return Task.CompletedTask;
    }

    public Task WriteFallbackAsync(string line, CancellationToken ct = default)
    {
        Fallback.Add(line);
        return Task.CompletedTask;
    }
}

/// <summary>内存撤销标记存储。</summary>
public sealed class FakeRevocationStore : IIdentityRevocationStore
{
    public HashSet<string> Revoked { get; } = new(StringComparer.Ordinal);
    public bool ThrowOnMark { get; set; }

    public Task MarkRevokedAsync(string subject, DateTimeOffset revokedAt, CancellationToken ct = default)
    {
        if (ThrowOnMark)
            throw new InvalidOperationException("revocation store unavailable");
        Revoked.Add(subject);
        return Task.CompletedTask;
    }

    public Task ClearRevokedAsync(string subject, CancellationToken ct = default)
    {
        Revoked.Remove(subject);
        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(string subject, CancellationToken ct = default)
        => Task.FromResult(Revoked.Contains(subject));
}

internal static class TestDefaults
{
    public static KeycloakAdminOptions Options() => new()
    {
        AdminBaseUrl = "http://localhost:8323",
        Realm = "apromisedland",
        AdminClientId = "treegraph-admin",
        AdminClientSecret = "admin-secret",
        StepUpMaxAgeSeconds = 300
    };

    public static ILogger<T> Logger<T>() => NullLogger<T>.Instance;

    public static AdminActor Actor(string sub = "admin-sub", string name = "admin")
        => new(sub, name, ["app-admin"]);

    public static IDistributedCache MemoryCache()
        => new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));

    /// <summary>构造真实 KeycloakAdminClient + AdminOperationRunner，供各服务测试复用。</summary>
    public static (KeycloakAdminClient Client, AdminOperationRunner Runner, FakeAdminAuditStore Audit)
        CreateServices(Func<HttpRequestMessage, HttpResponseMessage> adminResponder)
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("/token", StringComparison.Ordinal) == true)
                return StubHttpMessageHandler.Json("{\"access_token\":\"tok\",\"expires_in\":300}");
            return adminResponder(req);
        });
        var factory = new FakeHttpClientFactory(handler);
        var options = new TestOptionsMonitor<KeycloakAdminOptions>(Options());
        var tokenProvider = new AdminTokenProvider(
            factory, options, new TestTimeProvider(), Logger<AdminTokenProvider>());
        var client = new KeycloakAdminClient(
            factory, tokenProvider, options, Logger<KeycloakAdminClient>());
        var audit = new FakeAdminAuditStore();
        var runner = new AdminOperationRunner(audit, options, Logger<AdminOperationRunner>());
        return (client, runner, audit);
    }

    /// <summary>构造带 Location 头的创建成功响应。</summary>
    public static HttpResponseMessage Created(string location)
        => new(HttpStatusCode.Created)
        {
            Headers = { Location = new Uri(location) }
        };
}
