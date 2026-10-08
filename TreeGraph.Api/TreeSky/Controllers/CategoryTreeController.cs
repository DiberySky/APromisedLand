using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.TreeSky.Services;
using TreeGraph.Shared.TreeSky.Entities;
using TreeGraph.Shared.TreeSky.Models;

namespace TreeGraph.Api.TreeSky.Controllers;

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
}