using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.TreeEavSky.Controllers;

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
