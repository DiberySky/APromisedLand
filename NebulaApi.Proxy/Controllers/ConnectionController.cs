using Microsoft.AspNetCore.Mvc;
using NebulaApi.Proxy.Services;

namespace NebulaApi.Proxy.Controllers;

/// <summary>
/// Mirrors <c>app.routers.connection</c>: connectivity check and the
/// list of spaces visible to the FastAPI + nebula-python service.
/// </summary>
[ApiController]
[Route("connection")]
public sealed class ConnectionController : ControllerBase
{
    private readonly NebulaFastApiService _api;
    public ConnectionController(NebulaFastApiService api) => _api = api;

    /// <summary>NebulaGraph connectivity check.</summary>
    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var res = await _api.GetAsync("/connection/health", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>List all graph spaces.</summary>
    [HttpGet("spaces")]
    public async Task<IActionResult> ListSpaces(CancellationToken ct)
    {
        var res = await _api.GetAsync("/connection/spaces", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}
