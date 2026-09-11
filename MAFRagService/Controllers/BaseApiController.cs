using MAFRagService.Startup.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace MAFRagService.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected IConfiguration Config { get; }

    protected BaseApiController(IConfiguration config) => Config = config;

    /// <summary>懒加载 FeatureFlags，避免污染所有 Controller 构造函数。</summary>
    protected FeatureFlags Features =>
        HttpContext.RequestServices.GetRequiredService<FeatureFlags>();

    /// <summary>模块未启用时返回 503。</summary>
    protected IActionResult ModuleDisabled(string module)
        => StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { Error = $"模块 '{module}' 未启用。", Module = module });

    /// <summary>
    /// 租户解析（开发阶段：无身份验证）。
    ///   1) 请求头 X-Tenant-Id
    ///   2) 回退 "default"
    ///
    /// 生产环境恢复身份验证后，改为从 JWT claim 读取：
    ///   var claim = User.FindFirst("tenant")?.Value
    ///               ?? User.FindFirst(ClaimTypes.GroupSid)?.Value;
    ///   if (!string.IsNullOrEmpty(claim)) return claim;
    ///   （再走 X-Tenant-Id 回退，最后 default）
    /// </summary>
    protected string ResolveTenant()
    {
        var h = Request.Headers["X-Tenant-Id"].ToString();
        if (!string.IsNullOrEmpty(h)) return h;

        return "default";
    }
}