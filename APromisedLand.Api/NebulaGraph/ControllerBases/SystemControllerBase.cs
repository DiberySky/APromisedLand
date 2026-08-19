using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.NebulaGraph.ControllerBases;

/// <summary>
/// Local system endpoints. <c>/healthz</c> reports the .NET proxy's
/// own liveness so Kubernetes / load balancers can probe the proxy
/// independently of the upstream FastAPI service. The root <c>/</c>
/// path is mapped by <c>Program.cs</c> to a redirect to the Swagger
/// UI for discoverability.
/// </summary>
[ApiController]
public abstract class SystemControllerBase : ControllerBase
{
    /// <summary>Process liveness probe.</summary>
    [HttpGet("/healthz")]
    public IActionResult Healthz() => Ok(new { status = "alive" });
}
