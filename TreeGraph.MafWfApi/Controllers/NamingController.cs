using Microsoft.AspNetCore.Mvc;
using TreeGraph.MafWfApi.Models;
using TreeGraph.MafWfApi.Services;

namespace TreeGraph.MafWfApi.Controllers;

/// <summary>
/// 中文 → 英文变量名生成入口。
/// 路由前缀：/api/naming
/// 响应由 ApiEnvelopeFilter 统一包装为 { success, message, data }。
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class NamingController : ControllerBase
{
    private readonly NamingService _naming;
    private readonly ILogger<NamingController> _logger;

    public NamingController(NamingService naming, ILogger<NamingController> logger)
    {
        _naming = naming;
        _logger = logger;
    }

    /// <summary>根据中文名称生成 snake_case 与 PascalCase 变量名。</summary>
    /// <remarks>400 响应同样经信封包装：{ success:false, message:"请求参数有误", data:... }</remarks>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(NamingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<NamingResult>> Generate(
        [FromBody] NamingRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Naming request, TextLength={Len}", request.Text.Length);

        var result = await _naming.GenerateAsync(request.Text, ct);
        return Ok(result);
    }
}
