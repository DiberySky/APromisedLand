using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.SpaceNodeSky.Services;

namespace TreeGraph.Api.SpaceNodeSky.Controllers;

/// <summary>
/// Tag 投影计划端点：GET api/spaces/tag-projection-plan。
///
/// 供 SpaceNode UI「派生 Tag 只读区 / 未投影标记」消费：全部组合属性的投影状态
/// （projected / tombstoned / pending / unprojected + reason）。账本懒物化，
/// 状态由属性定义动态计算，与单一空间无关（版本不变量：全空间账本行共享同一 Version）。
/// 响应信封由 NodeEavSky 全局 ApiEnvelopeFilter 统一包装。
/// </summary>
[ApiController]
[Route("api/spaces/tag-projection-plan")]
public sealed class TagProjectionPlanController(TagProjectionService svc) : ControllerBase
{
    /// <summary>全部组合属性（DataType=composite）的投影计划。</summary>
    [HttpGet]
    public async Task<IActionResult> GetPlan(CancellationToken ct)
        => Ok(await svc.GetProjectionPlanAsync(ct));
}
