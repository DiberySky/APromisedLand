using APromisedLand.Api.Projects.DiberyTree;
using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DiberyTreeService.Controllers;

/// <summary>
/// 分类树 API 控制器
/// </summary>
[ApiController]
[Route("[controller]")]  // 路由: /api/CategoryTree
public class CategoryTreeController(
    ITreeService<CategoryTree> treeService,
    ITreeAttributeService attributeService,
    ILogger<CategoryTreeController> logger)
    : TreeControllerBase<CategoryTree>(treeService, attributeService, logger)
{
    // ==================== 自定义扩展端点 ====================

    /// <summary>
    /// 根据名称前缀搜索节点（示例扩展）
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>ApiResponse 格式的搜索结果</returns>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ApiResponse<object>))] // 根据实际返回类型调整
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiResponse<object>))]
    public async Task<IActionResult> SearchByName([FromQuery] string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest(ApiResponse<object>.Fail("搜索关键词不能为空"));

        try
        {
            // 使用 QueryNodesAsync 实现搜索，可自定义查询参数
            var queryParams = new TreeQueryParams
            {
                SearchTerm = keyword,
                Page = 1,
                PageSize = 100 // 可根据需要调整
            };
            var results = await TreeService.QueryNodesAsync(queryParams, cancellationToken);
            return Ok(ApiResponse<object>.Ok(results));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "搜索节点失败，关键词: {Keyword}", keyword);
            return StatusCode(500, ApiResponse<object>.Fail($"搜索节点时发生错误: {ex.Message}"));
        }
    }

    // 可以继续添加其他自定义端点，均使用 ApiResponse
}