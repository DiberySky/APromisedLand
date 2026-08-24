using APromisedLand.Api.DiberyTree.Controllers;
using APromisedLand.Api.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Mvc;

namespace DiberyTreeService.Controllers;

[ApiController]
[Route("[controller]")]  // 路由: /api/UnitTree
public class UnitTreeController(ITreeService<UnitTree> treeService, 
ILogger<UnitTreeController> logger)
: TreeControllerBase<UnitTree>(treeService, logger)
{
    
}