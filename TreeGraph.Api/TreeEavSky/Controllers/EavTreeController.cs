using Microsoft.AspNetCore.Mvc;
using TreeGraph.Api.TreeEavSky.Services;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.TreeEavSky.Controllers;

/// <summary>
/// EAV 树 API：路由 /EavTree
/// </summary>
[ApiController]
[Route("[controller]")]
public class EavTreeController(
    ITreeService<EavTree> treeService,
    ILogger<EavTreeController> logger)
    : TreeControllerBase<EavTree>(treeService, logger)
{
}
