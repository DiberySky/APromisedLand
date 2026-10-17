using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
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
            request.Query.Trim(), topK, request.SpaceId, ct);

        _logger.LogDebug(
            "语义搜索：query={Query} topK={TopK} hits={Count}",
            request.Query, topK, results.Count);

        return Ok(new SemanticSearchResponse(
            Query: request.Query.Trim(),
            Total: results.Count,
            Items: results));
    }

    /// <summary>
    /// 手动触发向量对账（立即执行，无需等待后台周期）。
    /// 用于修复：PG 有、Qdrant 无（补算向量）或 Qdrant 有、PG 无（删除孤儿）。
    /// </summary>
    [HttpPost("reconcile")]
    public async Task<IActionResult> ReconcileVectors(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var synchronizer = scope.ServiceProvider
            .GetRequiredService<VectorSynchronizer>();

        var result = await synchronizer.ReconcileAllAsync(ct);

        _logger.LogInformation(
            "手动向量对账：期望={Active} 已存={Existing} 缺失={Missing} 孤儿={Orphaned} 修复={Fixed} 删除={Deleted}",
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
}

public sealed class SemanticSearchRequest
{
    [Required]
    public string Query { get; set; } = "";
    public int? TopK { get; set; }
    public string? SpaceId { get; set; }
}

public sealed record SemanticSearchResponse(
    string Query, int Total,
    IReadOnlyList<SemanticSearchItemDto> Items);
