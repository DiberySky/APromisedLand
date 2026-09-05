
using APromisedLand.Api.Foundries;
using Microsoft.AI.Foundry.Local;
using Microsoft.AspNetCore.Mvc;

namespace FoundryLocalService.Controllers;

[ApiController]
[Route("[controller]")]   // 路由为 /api/FoundryLocal
public class FoundryLocalController(FoundryLocalManager mgr) : FoundryLocalControllerBase(mgr)
{
    // 基类已包含所有端点，此处为空即可
}