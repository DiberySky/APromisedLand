using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.TreeEavSky.Controllers;

/// <summary>
/// 空间树 API：路由 /SpaceTree
/// </summary>
[ApiController]
[Route("[controller]")]
public class SpaceTreeController(
    ITreeService<SpaceTree> treeService,
    ILogger<SpaceTreeController> logger)
    : TreeControllerBase<SpaceTree>(treeService, logger)
{
}
