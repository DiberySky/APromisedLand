using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky.Entities;
using TreeGraph.Shared.TreeEavSky.Models;

namespace TreeGraph.Api.TreeEavSky.Controllers;

/// <summary>
/// 分类树 API：路由 /CategoryTree
/// </summary>
[ApiController]
[Route("[controller]")]
public class CategoryTreeController(
    ITreeService<CategoryTree> treeService,
    ILogger<CategoryTreeController> logger)
    : TreeControllerBase<CategoryTree>(treeService, logger)
{
    // ==================== 自定义扩展端点 ====================

    /// <summary>
    /// 根据名称关键词搜索节点（内部走 QueryNodesAsync，最多返回 100 条）。
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="cancellationToken">取消令牌</param>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchByName([FromQuery] string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest(new { error = "搜索关键词不能为空" });

        try
        {
            var queryParams = new TreeQueryParams
            {
                SearchTerm = keyword,
                Page = 1,
                PageSize = 100
            };
            var results = await TreeService.QueryNodesAsync(queryParams, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "搜索节点失败，关键词: {Keyword}", keyword);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"搜索节点时发生错误: {ex.Message}" });
        }
    }
}
