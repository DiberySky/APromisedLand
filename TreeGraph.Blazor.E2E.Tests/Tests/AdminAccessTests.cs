using Microsoft.Playwright;
using TreeGraph.Blazor.E2E.Tests.Base;
using TreeGraph.Blazor.E2E.Tests.Fixtures;
using Xunit;

namespace TreeGraph.Blazor.E2E.Tests.Tests;

/// <summary>管理台入口的认证门禁冒烟用例（需 Aspire 全栈运行）。</summary>
public class AdminAccessTests : E2ETestBase
{
    public AdminAccessTests(PlaywrightFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task AdminUsers_RequiresAuthentication()
    {
        // 未携带会话访问 /admin/users 应被 OIDC Challenge 重定向到 Keycloak 登录端点。
        // 不能用 GoToAsync：它等待 Blazor 就绪，而重定向后的登录页没有 Blazor。
        await Page.GotoAsync("/admin/users", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        // Keycloak 授权端点会二次跳转到 login-actions/authenticate，最终 URL 不在 auth 路径上
        await Page.WaitForURLAsync("**/realms/apromisedland/**");

        Assert.Contains("localhost:8323/realms/apromisedland", Page.Url, StringComparison.Ordinal);
        await Page.WaitForSelectorAsync("#username");
    }

    [Fact]
    public async Task AdminRealm_RequiresAuthentication()
    {
        await Page.GotoAsync("/admin/realm", new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await Page.WaitForURLAsync("**/realms/apromisedland/**");

        Assert.Contains("localhost:8323/realms/apromisedland", Page.Url, StringComparison.Ordinal);
        await Page.WaitForSelectorAsync("#username");
    }
}
