using MafRagApi.Models;
using MafRagApi.Services.Tools;
using Microsoft.AspNetCore.Mvc;

namespace MafRagApi.Controllers;

[ApiController]
[Route("api/agent/tools")]
[Produces("application/json")]
public sealed class AgentToolsController(IToolRegistry registry) : ControllerBase
{
    [HttpGet]
    public ActionResult<ToolListDto> List()
    {
        var items = registry.List().Select(d => new ToolDescriptorDto
        {
            Name             = d.Name,
            Description      = d.Description,
            Tags             = d.Tags,
            ParametersSchema = DefaultToolRegistry.SerializeSchema(d.Function),
            SafeByDefault    = d.SafeByDefault,
            IsBase           = d.IsBase,
        }).ToArray();

        return Ok(new ToolListDto { Items = items });
    }
}