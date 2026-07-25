using APromisedLand.Api.Contracts.DiberyTree;
using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Mvc;

namespace DiberyTreeService.Controllers;

/// <summary>
/// 分类树 API 控制器
/// </summary>
[ApiController]
[Route("[controller]")]  // 路由: /api/CategoryTree
public class CategoryTreeController(ITreeService<CategoryTree> treeService, 
    ILogger<CategoryTreeController> logger)
    : TreeControllerBase<CategoryTree>(treeService, logger)
{
    // 基类已实现所有 CRUD，无需额外初始化

    // 您可以在此添加额外的自定义端点，例如：
    
    /// <summary>
    /// 根据名称前缀搜索节点（示例扩展）
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchByName([FromQuery] string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return BadRequest("搜索关键词不能为空");

        // 使用 QueryNodesAsync 实现搜索，可自定义查询参数
        var queryParams = new TreeQueryParams
        {
            SearchTerm = keyword,
            Page = 1,
            PageSize = 100 // 可根据需要调整
        };
        var results = await TreeService.QueryNodesAsync(queryParams, cancellationToken);
        return Ok(results);
    }
}