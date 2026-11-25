using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using TreeGraph.Api.Infrastructure;
using TreeGraph.Shared.Identity;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// <see cref="SpaceAuthorizationFilter"/> 单测：用假 ICurrentUser / 假 ISpaceResolver，
/// 不依赖数据库或完整 ASP.NET 管道。
/// </summary>
public class SpaceAuthorizationFilterTests
{
    private static AuthorizationFilterContext CreateContext(Action<RouteValueDictionary> configureRoute)
    {
        var routeData = new RouteData();
        configureRoute(routeData.Values);
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            routeData,
            new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public async Task Route_SpaceId_Matching_User_Passes()
    {
        var filter = new SpaceAuthorizationFilter(
            new FakeCurrentUser { SpaceIds = new[] { "space-1001" }, IsAdmin = false },
            new FakeSpaceResolver());
        var context = CreateContext(v => v["spaceId"] = "1001");

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Route_SpaceId_Not_Matching_Returns_403()
    {
        var filter = new SpaceAuthorizationFilter(
            new FakeCurrentUser { SpaceIds = new[] { "space-1001" }, IsAdmin = false },
            new FakeSpaceResolver());
        var context = CreateContext(v => v["spaceId"] = "2002");

        await filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Admin_Bypasses_Filter()
    {
        var filter = new SpaceAuthorizationFilter(
            new FakeCurrentUser { SpaceIds = Array.Empty<string>(), IsAdmin = true },
            new FakeSpaceResolver());
        var context = CreateContext(v => v["spaceId"] = "anything");

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task No_SpaceId_And_No_Node_Key_Passes()
    {
        var filter = new SpaceAuthorizationFilter(
            new FakeCurrentUser { SpaceIds = new[] { "space-1001" }, IsAdmin = false },
            new FakeSpaceResolver());
        var context = CreateContext(_ => { });

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Node_Key_Resolves_Matching_Space_Passes()
    {
        var resolver = new FakeSpaceResolver { Resolved = "1001" };
        var filter = new SpaceAuthorizationFilter(
            new FakeCurrentUser { SpaceIds = new[] { "space-1001" }, IsAdmin = false },
            resolver,
            nodeRouteKey: "nodeId");
        var context = CreateContext(v => v["nodeId"] = "node-7");

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Node_Key_Resolves_Non_Matching_Space_Returns_403()
    {
        var resolver = new FakeSpaceResolver { Resolved = "2002" };
        var filter = new SpaceAuthorizationFilter(
            new FakeCurrentUser { SpaceIds = new[] { "space-1001" }, IsAdmin = false },
            resolver,
            nodeRouteKey: "nodeId");
        var context = CreateContext(v => v["nodeId"] = "node-7");

        await filter.OnAuthorizationAsync(context);

        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task Resolver_Returns_Null_Passes_No_Constraint()
    {
        // 解析不到空间时放行，交由端点自身 404 语义处理。
        var resolver = new FakeSpaceResolver { Resolved = null };
        var filter = new SpaceAuthorizationFilter(
            new FakeCurrentUser { SpaceIds = Array.Empty<string>(), IsAdmin = false },
            resolver,
            nodeRouteKey: "nodeId");
        var context = CreateContext(v => v["nodeId"] = "node-x");

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public string UserId => "u1";
        public string? UserName => "tester";
        public required IReadOnlyList<string> SpaceIds { get; init; }
        public bool IsAdmin { get; init; }
        public IReadOnlyList<string> Roles => IsAdmin ? new[] { IdentityRoles.Admin } : Array.Empty<string>();
        public bool IsInRole(string role) => role == IdentityRoles.Admin && IsAdmin;
    }

    private sealed class FakeSpaceResolver : ISpaceResolver
    {
        public string? Resolved { get; set; }
        public Task<string?> ResolveSpaceIdAsync(string nodeId, CancellationToken ct = default)
            => Task.FromResult(Resolved);
    }
}
