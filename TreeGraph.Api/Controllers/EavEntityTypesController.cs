using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.Controllers;

/// <summary>
/// 实体类型元数据端点。
///
/// 独立于 EavController（其路由 api/eav/{entityType} 会与 api/eav/entity-types 冲突），
/// 因此拆到单独 Controller。
/// </summary>
[ApiController]
[Route("api/eav/entity-types")]
public class EavEntityTypesController : ControllerBase
{
    private readonly EavDbContext _db;
    private readonly IAttributeCache _attrCache;

    public EavEntityTypesController(EavDbContext db, IAttributeCache attrCache)
    {
        _db = db;
        _attrCache = attrCache;
    }

    /// <summary>列出所有已定义的实体类型（含属性计数）</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        // 直接从 AttributeCatalog 聚合
        var rows = await _db.AttributeCatalog
            .Where(a => !a.IsDeleted)
            .GroupBy(a => a.EntityType)
            .Select(g => new
            {
                EntityType = g.Key,
                Total = g.Count(),
                Searchable = g.Count(a => a.IsSearchable),
                FirstDisplay = g.OrderBy(a => a.DisplayOrder)
                                .Select(a => a.DisplayName)
                                .FirstOrDefault()
            })
            .OrderBy(x => x.EntityType)
            .AsNoTracking()
            .ToListAsync(ct);

        var result = rows.Select(r => new EntityTypeSummaryDto(
            r.EntityType, r.Total, r.Searchable, r.FirstDisplay));

        return Ok(result);
    }
}
