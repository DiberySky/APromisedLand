using Hangfire.Dashboard;

namespace MAFRagService.Tools;

/// <summary>
/// Hangfire Dashboard 访问控制：仅允许已通过 JWT 认证的用户访问。
/// 需要引用 <c>Hangfire.AspNetCore</c> 包以获取 <c>GetHttpContext()</c> 扩展方法。
/// </summary>
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // GetHttpContext 扩展方法来自 Hangfire.AspNetCore 包
        var http = context.GetHttpContext();

        // 最小约束：必须已认证
        if (http.User.Identity?.IsAuthenticated != true)
            return false;

        // 严格模式（可选）：要求 Admin 角色
        // return http.User.IsInRole("Admin");

        return true;
    }
}