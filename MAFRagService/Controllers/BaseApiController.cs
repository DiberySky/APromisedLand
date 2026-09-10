using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace MAFRagService.Controllers;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected IConfiguration Config { get; }

    protected BaseApiController(IConfiguration config)
    {
        Config = config;
    }

    /// <summary>
    /// 租户解析。对应原 Program.cs 中的局部函数 ResolveTenant。
    ///   1) JWT 的 "tenant" / GroupSid claim
    ///   2) 开发环境下回退到请求头 X-Tenant-Id
    ///   3) 最终回退 "default"
    /// </summary>
    protected string ResolveTenant()
    {
        var claim = User.FindFirst("tenant")?.Value
                    ?? User.FindFirst(ClaimTypes.GroupSid)?.Value;
        if (!string.IsNullOrEmpty(claim)) return claim;

        var env = Config["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        if (env.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            var h = Request.Headers["X-Tenant-Id"].ToString();
            if (!string.IsNullOrEmpty(h)) return h;
        }

        return "default";
    }
}