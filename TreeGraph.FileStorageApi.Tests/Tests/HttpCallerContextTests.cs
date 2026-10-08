using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using TreeGraph.FileStorageApi.Security;   // InvalidTenantException 位于 Security 命名空间
using TreeGraph.FileStorageApi.Storage;   // HttpCallerContext 位于 Storage 命名空间（与目录交叉）
using Xunit;

namespace TreeGraph.FileStorageApi.Tests;

/// <summary>
/// HttpCallerContext 纯单测：Claim → 请求头 → 默认值 的取值优先级、
/// 长度护栏（tenant 128 抛出 / actor 256 截断）与认证状态。
/// </summary>
public sealed class HttpCallerContextTests
{
    private static HttpCallerContext Create(DefaultHttpContext? http = null)
        => new(new HttpContextAccessor { HttpContext = http });

    private static DefaultHttpContext Http(Action<DefaultHttpContext> setup)
    {
        var http = new DefaultHttpContext();
        setup(http);
        return http;
    }

    // ── 维度 1：Tenant 取值优先级 ──

    [Fact]
    public void Tenant_NoHttpContext_ReturnsDefault()
    {
        Assert.Equal("default", Create().Tenant);
    }

    [Fact]
    public void Tenant_FallsBackToHeader_ThenDefault()
    {
        var withHeader = Http(http =>
            http.Request.Headers["X-Tenant-Id"] = "tenant-from-header");
        Assert.Equal("tenant-from-header", Create(withHeader).Tenant);

        Assert.Equal("default", Create(new DefaultHttpContext()).Tenant);
    }

    [Fact]
    public void Tenant_ClaimOverridesHeader()
    {
        var http = Http(http =>
        {
            http.Request.Headers["X-Tenant-Id"] = "tenant-from-header";
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenant", "tenant-from-claim"),
            ]));
        });

        Assert.Equal("tenant-from-claim", Create(http).Tenant);
    }

    [Fact]
    public void Tenant_EmptyOrWhitespaceHeader_ReturnsDefault()
    {
        var http = Http(http => http.Request.Headers["X-Tenant-Id"] = "   ");
        Assert.Equal("default", Create(http).Tenant);
    }

    // ── 维度 2：Tenant 长度护栏 ──

    [Fact]
    public void Tenant_Over128Chars_ThrowsInvalidTenant()
    {
        var http = Http(http =>
            http.Request.Headers["X-Tenant-Id"] = new string('x', 129));

        Assert.Throws<InvalidTenantException>(() => Create(http).Tenant);
    }

    [Fact]
    public void Tenant_Exactly128Chars_Accepted()
    {
        var http = Http(http =>
            http.Request.Headers["X-Tenant-Id"] = new string('x', 128));

        Assert.Equal(128, Create(http).Tenant.Length);
    }

    // ── 维度 3：Actor ──

    [Fact]
    public void Actor_ClaimNameOverridesHeader_NullWhenAbsent()
    {
        Assert.Null(Create(new DefaultHttpContext()).Actor);

        var withHeader = Http(http => http.Request.Headers["X-Actor"] = "actor-header");
        Assert.Equal("actor-header", Create(withHeader).Actor);

        var withBoth = Http(http =>
        {
            http.Request.Headers["X-Actor"] = "actor-header";
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimsIdentity.DefaultNameClaimType, "actor-claim"),
            ]));
        });
        Assert.Equal("actor-claim", Create(withBoth).Actor);
    }

    [Fact]
    public void Actor_Over256Chars_Truncated()
    {
        var http = Http(http =>
            http.Request.Headers["X-Actor"] = new string('a', 300));

        var actor = Create(http).Actor;
        Assert.NotNull(actor);
        Assert.Equal(256, actor.Length);
        Assert.Equal(new string('a', 256), actor);
    }

    // ── 维度 4：认证状态 ──

    [Fact]
    public void IsAuthenticated_ReflectsIdentity()
    {
        Assert.False(Create(new DefaultHttpContext()).IsAuthenticated);

        var authenticated = Http(http =>
            http.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("a", "b")], "Test")));
        Assert.True(Create(authenticated).IsAuthenticated);
    }
}
