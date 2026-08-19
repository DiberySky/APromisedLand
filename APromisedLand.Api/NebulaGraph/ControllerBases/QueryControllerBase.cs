using APromisedLand.Api.NebulaGraph.Dtos;
using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.NebulaGraph.ControllerBases;

/// <summary>
/// Mirrors <c>app.routers.query</c>: raw nGQL, GO, LOOKUP, FIND PATH,
/// GET SUBGRAPH, EXPLAIN / PROFILE and per-space statistics.
/// </summary>
[ApiController]
[Route("query")]
public abstract class QueryControllerBase(NebulaFastApiService api) : ControllerBase
{
    /// <summary>Execute raw nGQL and return parsed rows.</summary>
    [HttpPost("ngql")]
    public async Task<IActionResult> RawNgql([FromBody] RawStmtIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/query/ngql", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>EXPLAIN a statement (return plan).</summary>
    [HttpPost("explain")]
    public async Task<IActionResult> Explain([FromBody] RawStmtIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/query/explain", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>PROFILE a statement (return plan + latency).</summary>
    [HttpPost("profile")]
    public async Task<IActionResult> Profile([FromBody] RawStmtIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/query/profile", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Run a GO traversal.</summary>
    [HttpPost("go")]
    public async Task<IActionResult> Go([FromBody] GoIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/query/go", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Run a LOOKUP (index-based search).</summary>
    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup([FromBody] LookupIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/query/lookup", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Find paths between two vertices.</summary>
    [HttpPost("find-path")]
    public async Task<IActionResult> FindPath([FromBody] FindPathIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/query/find-path", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Get a subgraph around a vertex.</summary>
    [HttpPost("subgraph")]
    public async Task<IActionResult> GetSubgraph([FromBody] GetSubgraphIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/query/subgraph", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Show space statistics.</summary>
    [HttpGet("stats/{space}")]
    public async Task<IActionResult> Stats(string space, CancellationToken ct)
    {
        var res = await api.GetAsync(
            $"/query/stats/{Uri.EscapeDataString(space)}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}
