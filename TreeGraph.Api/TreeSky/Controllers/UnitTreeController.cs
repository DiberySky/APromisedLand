using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.TreeSky.Services;
using TreeGraph.Shared.TreeSky.Entities;

namespace TreeGraph.Api.TreeSky.Controllers;

/// <summary>
/// 计量单位树 API：路由 /UnitTree
/// </summary>
[ApiController]
[Route("[controller]")]
public class UnitTreeController(
    ITreeService<UnitTree> treeService,
    ILogger<UnitTreeController> logger)
    : TreeControllerBase<UnitTree>(treeService, logger)
{
}
