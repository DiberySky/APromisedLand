using APromisedLand.Api.NebulaGraph.ControllerBases;
using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;

namespace NebulaApi.Proxy.Controllers;

/// <summary>
/// 分类树 API 控制器
/// </summary>
[ApiController]
[Route("[controller]")]  // 路由: /api/CategoryTree
public sealed class NebulaGraphController(NebulaFastApiService api)
    : ConnectionControllerBase(api)
{
}