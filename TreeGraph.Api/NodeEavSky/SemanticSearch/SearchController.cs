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

    public SearchController(
        SearchService searchService,
        IOptions<SemanticSearchOptions> options,
        ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _options = options.Value;
        _logger = logger;
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
