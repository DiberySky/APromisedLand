using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TreeGraph.Api.GraphSyncSky;
using TreeGraph.Api.NodeEavSky.SemanticSearch;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

/// <summary>
/// 语义搜索 API。
/// 路由：POST /api/search/semantic
/// 响应信封由 NodeEavSky 全局 ApiEnvelopeFilter 统一包装：
///   { success, message, data: { query, total, items } }
/// </summary>
[ApiController]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly SearchService _searchService;
    private readonly SemanticSearchOptions _options;
    private readonly ILogger<SearchController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public SearchController(
        SearchService searchService,
        IOptions<SemanticSearchOptions> options,
        ILogger<SearchController> logger,
        IServiceScopeFactory scopeFactory)
    {
        _searchService = searchService;
        _options = options.Value;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    [HttpPost("semantic")]
    public async Task<IActionResult> SemanticSearch(
        [FromBody] SemanticSearchRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "查询文本不能为空" });

        var topK = Math.Clamp(
            request.TopK ?? _options.DefaultTopK, 1, _options.MaxTopK);

        var results = await _searchService.SearchAsync(
            request.Query.Trim(), topK, request.SpaceId, request.MinScore, ct);

        _logger.LogDebug(
            "语义搜索：query={Query} topK={TopK} minScore={MinScore} hits={Count}",
            request.Query, topK, request.MinScore, results.Count);

        return Ok(new SemanticSearchResponse(
            Query: request.Query.Trim(),
            Total: results.Count,
            Items: results));
    }

    /// <summary>
    /// 手动触发向量对账（立即执行，无需等待后台周期）。
    /// 用于修复：PG 有、Qdrant 无（补算向量）或 Qdrant 有、PG 无（删除孤儿）。
    /// 范围四选一：category（space=空间树；tree=单位/分类/知识树；line=线路；边无向量不支持）
    /// > spaceId（该空间对应树范围）> lineId（该线路自身+其线路节点）> 全量（三者皆空）。
    /// </summary>
    [HttpPost("reconcile")]
    public async Task<IActionResult> ReconcileVectors(
        [FromQuery] string? spaceId, [FromQuery] string? category,
        [FromQuery] string? lineId, CancellationToken ct)
    {
        var scopeCount = (string.IsNullOrWhiteSpace(category) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(spaceId) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(lineId) ? 0 : 1);
        if (scopeCount > 1)
            return BadRequest(new { error = "category / spaceId / lineId 只能三选一" });

        SyncCategory? syncCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!SyncCategoryParser.TryParse(category, out var parsed) || parsed == SyncCategory.Edge)
                return BadRequest(new { error = "category 仅支持 space|tree|line（边无向量）" });
            syncCategory = parsed;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var synchronizer = scope.ServiceProvider
            .GetRequiredService<VectorSynchronizer>();

        var result = syncCategory is not null
            ? await synchronizer.ReconcileCategoryAsync(syncCategory.Value, ct)
            : !string.IsNullOrWhiteSpace(spaceId)
                ? await synchronizer.ReconcileSpaceAsync(spaceId, ct)
                : !string.IsNullOrWhiteSpace(lineId)
                    ? await synchronizer.ReconcileLineAsync(lineId, ct)
                    : await synchronizer.ReconcileAllAsync(ct);

        _logger.LogInformation(
            "手动向量对账（空间={SpaceId} 类别={Category} 线路={LineId}）：期望={Active} 已存={Existing} 缺失={Missing} 孤儿={Orphaned} 修复={Fixed} 删除={Deleted}",
            spaceId ?? "*", syncCategory?.ToString() ?? "*", lineId ?? "*",
            result.Active, result.Existing, result.Missing,
            result.Orphaned, result.FixedCount, result.DeletedCount);

        return Ok(new
        {
            active = result.Active,
            existing = result.Existing,
            missing = result.Missing,
            orphaned = result.Orphaned,
            fixedCount = result.FixedCount,
            deletedCount = result.DeletedCount
        });
    }

    /// <summary>
    /// 流式向量对账（NDJSON 逐行推送进度，同步刷新对话框滚动展示用）。
    /// 行格式：{"stage":"fix","inodeId":...,"name":...} 同步节点；
    ///        {"stage":"purge",...,"detail":...} 清理孤儿；
    ///        {"stage":"summary",...六个计数字段} 结束统计。
    /// 范围四选一：category（space=空间树；tree=单位/分类/知识树；line=线路）> spaceId > lineId > 全量。
    /// 校验失败仍走普通 400 信封；成功时直接写响应体并返回 EmptyResult，不经信封包装。
    /// </summary>
    [HttpPost("reconcile/stream")]
    public async Task<IActionResult> ReconcileVectorsStream(
        [FromQuery] string? spaceId, [FromQuery] string? category,
        [FromQuery] string? lineId, CancellationToken ct)
    {
        var scopeCount = (string.IsNullOrWhiteSpace(category) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(spaceId) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(lineId) ? 0 : 1);
        if (scopeCount > 1)
            return BadRequest(new { error = "category / spaceId / lineId 只能三选一" });

        SyncCategory? syncCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!SyncCategoryParser.TryParse(category, out var parsed) || parsed == SyncCategory.Edge)
                return BadRequest(new { error = "category 仅支持 space|tree|line（边无向量）" });
            syncCategory = parsed;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var synchronizer = scope.ServiceProvider
            .GetRequiredService<VectorSynchronizer>();

        Response.ContentType = "application/x-ndjson; charset=utf-8";

        // 必须逐行 await 写入再 flush：fire-and-forget 的并发写响应体会导致流卡死
        async Task WriteEventAsync(ReconcileStreamEventDto evt)
        {
            await Response.WriteAsync(
                System.Text.Json.JsonSerializer.Serialize(evt, JsonOpts) + "\n", ct);
            await Response.Body.FlushAsync(ct);
        }

        var result = await synchronizer.ReconcileAsync(
            string.IsNullOrWhiteSpace(spaceId) ? null : spaceId, syncCategory,
            string.IsNullOrWhiteSpace(lineId) ? null : lineId,
            onProgress: async p => await WriteEventAsync(new ReconcileStreamEventDto(
                p.Stage, p.InodeId, p.Name, p.Detail)),
            ct);

        await WriteEventAsync(new ReconcileStreamEventDto(
            "summary", "", "", "",
            result.Active, result.Existing, result.Missing, result.Orphaned,
            result.FixedCount, result.DeletedCount));

        _logger.LogInformation(
            "手动向量对账-流式（空间={SpaceId} 类别={Category} 线路={LineId}）：期望={Active} 已存={Existing} 缺失={Missing} 孤儿={Orphaned} 修复={Fixed} 删除={Deleted}",
            spaceId ?? "*", syncCategory?.ToString() ?? "*", lineId ?? "*",
            result.Active, result.Existing, result.Missing,
            result.Orphaned, result.FixedCount, result.DeletedCount);

        return new EmptyResult(); // 自写响应体，不经信封包装
    }

    private static readonly System.Text.Json.JsonSerializerOptions JsonOpts =
        new(System.Text.Json.JsonSerializerDefaults.Web);
}

public sealed class SemanticSearchRequest
{
    [Required]
    public string Query { get; set; } = "";
    public int? TopK { get; set; }
    public string? SpaceId { get; set; }

    /// <summary>相似度绝对下限（0~1）；为空则用 SemanticSearch:MinScore 配置默认值。</summary>
    public float? MinScore { get; set; }
}

public sealed record SemanticSearchResponse(
    string Query, int Total,
    IReadOnlyList<SemanticSearchItemDto> Items);
