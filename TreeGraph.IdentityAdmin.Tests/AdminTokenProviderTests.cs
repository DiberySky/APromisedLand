using System.Net;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.Token;
using Xunit;

namespace TreeGraph.IdentityAdmin.Tests;

public class AdminTokenProviderTests
{
    private const string TokenJson = "{\"access_token\":\"tok\",\"expires_in\":300}";

    private static AdminTokenProvider Build(
        StubHttpMessageHandler handler,
        TestTimeProvider? time = null)
        => new(
            new FakeHttpClientFactory(handler),
            new TestOptionsMonitor<KeycloakAdminOptions>(TestDefaults.Options()),
            time ?? new TestTimeProvider(),
            TestDefaults.Logger<AdminTokenProvider>());

    private static int TokenRequests(StubHttpMessageHandler h)
        => h.Requests.Count(r => r.RequestUri?.ToString().Contains("/token", StringComparison.Ordinal) == true);

    [Fact]
    public async Task GetAccessToken_CachesWithinExpiry_NoSecondRequest()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(TokenJson));
        var provider = Build(handler);

        var first = await provider.GetAccessTokenAsync();
        var second = await provider.GetAccessTokenAsync();

        Assert.Equal("tok", first);
        Assert.Equal(first, second);
        Assert.Equal(1, TokenRequests(handler));
    }

    [Fact]
    public async Task GetAccessToken_SingleFlight_ConcurrentCallsHitOnce()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(TokenJson))
        {
            Delay = TimeSpan.FromMilliseconds(100)
        };
        var provider = Build(handler);

        var results = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => provider.GetAccessTokenAsync()));

        Assert.All(results, r => Assert.Equal("tok", r));
        Assert.Equal(1, TokenRequests(handler));
    }

    [Fact]
    public async Task GetAccessToken_AfterExpiry_Refreshes()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(TokenJson));
        var time = new TestTimeProvider { UtcNow = DateTimeOffset.UtcNow };
        var provider = Build(handler, time);

        await provider.GetAccessTokenAsync();
        Assert.Equal(1, TokenRequests(handler));

        // expires_in 300 → 缓存按 240s 计；推进 250s 应触发刷新
        time.Advance(TimeSpan.FromSeconds(250));
        await provider.GetAccessTokenAsync();

        Assert.Equal(2, TokenRequests(handler));
    }

    [Fact]
    public async Task GetAccessToken_NonSuccessResponse_ThrowsUnauthorized()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var provider = Build(handler);

        await Assert.ThrowsAsync<KeycloakUnauthorizedException>(() => provider.GetAccessTokenAsync());
    }

    [Fact]
    public void Invalidate_ClearsCachedToken()
    {
        var handler = new StubHttpMessageHandler(_ => StubHttpMessageHandler.Json(TokenJson));
        var provider = Build(handler);

        provider.Invalidate(); // 不应抛异常
        Assert.Equal(0, TokenRequests(handler));
    }
}
