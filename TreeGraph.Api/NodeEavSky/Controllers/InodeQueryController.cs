using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.NodeEavSky.Services;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

/// <summary>
/// 跨 iNode 查询。
/// </summary>
[ApiController]
[Route("api/inode")]
public class InodeQueryController : ControllerBase
{
    private readonly EavQueryService _query;
    private readonly IInodeEntityService _inodeEntities;
    private readonly EavReadService _read;

    public InodeQueryController(
        EavQueryService query,
        IInodeEntityService inodeEntities,
        EavReadService read)
    {
        _query = query;
        _inodeEntities = inodeEntities;
        _read = read;
    }

    /// <summary>
    /// 跨 iNode 查询（可选限定 inodeId）。
    /// 请求体：
    /// {
    ///   "inodeId": "可选",
    ///   "entityType": "Product",      // 类型名
    ///   "filters": [...],
    ///   "orderByAttribute": "可选",
    ///   "orderDescending": false,
    ///   "page": 1,
    ///   "pageSize": 20
    /// }
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> Query(
        [FromBody] InodeQueryRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.EntityType))
            return BadRequest(new { error = "entityType 必填" });

        // 1. 确定候选 entity_id 集合
        IReadOnlyCollection<string>? allowedEntityIds = null;

        if (!string.IsNullOrEmpty(request.InodeId))
        {
            var mappings = await _inodeEntities.ListByInodeAsync(
                request.InodeId, ct);
            allowedEntityIds = mappings
                .Where(m => m.EntityType == request.EntityType)
                .Select(m => m.EntityId)
                .ToList();

            if (allowedEntityIds.Count == 0)
            {
                return Ok(new PagedResult<InodeEntityDto>
                {
                    Items = new List<InodeEntityDto>(),
                    Total = 0,
                    Page = request.Page,
                    PageSize = request.PageSize
                });
            }
        }

        // 2. 走查询
        var internalReq = new Shared.Eav.Dtos.EavQueryRequest
        {
            EntityType = request.EntityType,
            Filters = request.Filters,
            OrderByAttribute = request.OrderByAttribute,
            OrderDescending = request.OrderDescending,
            Page = request.Page,
            PageSize = request.PageSize
        };

        try
        {
            var result = await _query.QueryWithAllowedIdsAsync(
                request.EntityType, allowedEntityIds, internalReq, ct);

            // 3. 反查 inode_id（若跨 iNode 查询，需要给每个结果填充）
            var entityIds = result.Items.Select(e => e.EntityId).ToList();
            var inodeMap = await BuildInodeMapAsync(
                entityIds, request.EntityType, request.InodeId, ct);

            var dto = new PagedResult<InodeEntityDto>
            {
                Items = result.Items.Select(e => new InodeEntityDto(
                    inodeMap.GetValueOrDefault(e.EntityId, ""),
                    e.EntityId,
                    e.EntityType,
                    ToJsonDict(e),
                    e.UpdatedAt)).ToList(),
                Total = result.Total,
                Page = result.Page,
                PageSize = result.PageSize
            };

            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // ============================================================
    // 辅助
    // ============================================================

    private async Task<Dictionary<string, string>> BuildInodeMapAsync(
        IEnumerable<string> entityIds, string entityType,
        string? inodeId, CancellationToken ct)
    {
        var map = new Dictionary<string, string>();

        // 已指定 inodeId 时无需反查
        if (!string.IsNullOrEmpty(inodeId))
        {
            foreach (var id in entityIds)
                map[id] = inodeId;
            return map;
        }

        // 跨 iNode：逐个反查
        foreach (var id in entityIds)
        {
            var mapping = await _inodeEntities.GetByEntityAsync(
                entityType, id, ct);
            map[id] = mapping?.InodeId ?? "";
        }
        return map;
    }

    private static Dictionary<string, JsonElement> ToJsonDict(
        DynamicEntity e)
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var dict = new Dictionary<string, JsonElement>();
        foreach (var (k, v) in e.Properties)
        {
            dict[k] = v is null
                ? JsonDocument.Parse("null").RootElement.Clone()
                : JsonSerializer.SerializeToElement(v, v.GetType(), opts);
        }
        return dict;
    }
}
