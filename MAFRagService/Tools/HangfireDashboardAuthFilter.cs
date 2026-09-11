using Hangfire.Dashboard;

namespace MAFRagService.Tools;

/// <summary>
/// Hangfire Dashboard 访问控制（开发阶段：无身份验证，全放行）。
///
/// 生产环境恢复：
/// <code>
/// var http = context.GetHttpContext();
/// if (http.User.Identity?.IsAuthenticated != true) return false;
/// return http.User.IsInRole("Admin");   // 可选：角色校验
/// </code>
/// </summary>
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;   // 开发阶段：全放行
    }
}