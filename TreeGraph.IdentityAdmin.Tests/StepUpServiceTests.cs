using System.Security.Claims;
using TreeGraph.IdentityAdmin.Options;
using TreeGraph.IdentityAdmin.StepUp;
using TreeGraph.Shared.Identity.Admin;
using Xunit;

namespace TreeGraph.IdentityAdmin.Tests;

public class StepUpServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static StepUpService Build(out TestTimeProvider time, out TestOptionsMonitor<KeycloakAdminOptions> options)
    {
        time = new TestTimeProvider { UtcNow = Now };
        options = new TestOptionsMonitor<KeycloakAdminOptions>(TestDefaults.Options());
        return new StepUpService(TestDefaults.MemoryCache(), options, time);
    }

    private static ClaimsPrincipal Principal(long authTimeSeconds)
        => new(new ClaimsIdentity(new[] { new Claim("auth_time", authTimeSeconds.ToString()) }));

    [Fact]
    public void GetState_FreshWhenWithinMaxAge()
    {
        var svc = Build(out var time, out _);
        var state = svc.GetState(Principal(time.UtcNow.ToUnixTimeSeconds() - 100));

        Assert.True(state.IsFresh);
        Assert.NotNull(state.AuthTime);
        Assert.Equal(300, state.MaxAgeSeconds);
    }

    [Fact]
    public void GetState_NotFreshWhenBeyondMaxAge()
    {
        var svc = Build(out var time, out _);
        var state = svc.GetState(Principal(time.UtcNow.ToUnixTimeSeconds() - 400));

        Assert.False(state.IsFresh);
    }

    [Fact]
    public void GetState_NoAuthTimeClaim_NotFresh()
    {
        var svc = Build(out _, out _);
        var state = svc.GetState(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(state.IsFresh);
        Assert.Null(state.AuthTime);
    }

    [Fact]
    public async Task Ticket_IsSingleUse()
    {
        var svc = Build(out _, out _);
        var actor = TestDefaults.Actor("sub-1", "admin");

        var created = await svc.CreateTicketAsync(actor, "user.disable", null, "/admin");
        Assert.NotNull(created);

        var first = await svc.ConsumeTicketAsync(actor, created!.Token);
        Assert.NotNull(first);
        Assert.Equal("user.disable", first!.Action);
        Assert.Equal("/admin", first.ReturnUrl);

        var second = await svc.ConsumeTicketAsync(actor, created.Token);
        Assert.Null(second);
    }

    [Fact]
    public async Task ConsumeTicket_WrongActor_ReturnsNull()
    {
        var svc = Build(out _, out _);
        var owner = TestDefaults.Actor("sub-1", "admin");
        var created = await svc.CreateTicketAsync(owner, "x", null, null);

        var other = new AdminActor("sub-2", "other", []);
        Assert.Null(await svc.ConsumeTicketAsync(other, created!.Token));
    }

    [Fact]
    public async Task ConsumeTicket_Expired_ReturnsNull()
    {
        var svc = Build(out var time, out _);
        var actor = TestDefaults.Actor("sub-1", "admin");
        var created = await svc.CreateTicketAsync(actor, "y", null, null);

        time.Advance(TimeSpan.FromMinutes(6));

        Assert.Null(await svc.ConsumeTicketAsync(actor, created!.Token));
    }
}
