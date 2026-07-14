
using APromisedLand.Api.Contracts.DiberyTree;
using APromisedLand.Shared.DTOs;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Mvc;

namespace DiberyTreeService.Controllers;

/// <summary>
/// 专门用于 string 类型节点值的控制器
/// 路由为 api/stringtree (可通过 [Route] 自定义)
/// </summary>
[Route("[controller]")]
public class CategoryTreeController(
    ITreeService<Category> treeService,
    ILogger<CategoryTreeController> logger)
    : TreeControllerBase<Category>(treeService, logger);