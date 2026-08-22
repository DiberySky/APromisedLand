using APromisedLand.Api.Projects.DiberyTree;
using APromisedLand.Api.Projects.DiberyTree.Controllers;
using APromisedLand.Api.Projects.DiberyTree.Interface;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Mvc;

namespace DiberyTreeService.Controllers;

[ApiController]
[Route("[controller]")]  // 路由: /api/UnitTree
public class UnitTreeController(ITreeService<UnitTree> treeService, 
ITreeAttributeService attributeService,
ILogger<UnitTreeController> logger)
: TreeControllerBase<UnitTree>(treeService, attributeService, logger)
{
    
}