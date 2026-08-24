using APromisedLand.Api.DiberyTree.Interface;
using APromisedLand.Api.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Api.DiberyTree.Controllers;

/// <summary>
/// 属性定义控制器基类。从 <see cref="TreeControllerBase{T}"/> 分离——
/// 属性定义（schema）不耦合具体树，作为全局资源独立路由（definitions/types）。
/// <para>属性值端点（依赖 nodeId）仍保留在 <see cref="TreeControllerBase{T}"/>。</para>
/// <para>派生类只需继承并配置路由前缀，例如 <c>AttributesController : AttributeControllerBase</c>。</para>
/// </summary>
[ApiController]
[Route("[controller]")]
public abstract partial class AttributesControllerBase(
    AttributeService attributeService,
    ILogger<AttributesControllerBase> logger) : ControllerBase
{
    
}
