using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.NebulaGraph.ControllerBases;

/// <summary>
/// Mirrors <c>app.routers.connection</c>: connectivity check and the
/// list of spaces visible to the FastAPI + nebula-python service.
/// </summary>
[ApiController]
[Route("connection")]
public abstract  class ConnectionControllerBase(NebulaFastApiService api) : ControllerBase
{
    /// <summary>NebulaGraph connectivity check.</summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var res = await api.GetAsync("/connection/health", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>List all graph spaces.</summary>
    [HttpGet("spaces")]
    public async Task<IActionResult> ListSpaces(CancellationToken ct)
    {
        var res = await api.GetAsync("/connection/spaces", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}
