using APromisedLand.Api.NebulaGraph.Dtos;
using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.NebulaGraph.ControllerBases;

/// <summary>
/// Mirrors <c>app.routers.jobs</c>: compact / flush / stats
/// submission, listing, inspection, stop and recover.
/// </summary>
[ApiController]
[Route("NebulaGraph/jobs")]
public abstract class JobsControllerBase(NebulaFastApiService api) : ControllerBase
{
    /// <summary>Submit a compact job.</summary>
    [HttpPost("compact")]
    public async Task<IActionResult> Compact([FromBody] CompactIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/jobs/compact", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Submit a flush job.</summary>
    [HttpPost("flush")]
    public async Task<IActionResult> Flush([FromBody] FlushIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/jobs/flush", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Submit a stats job for a space.</summary>
    [HttpPost("stats/{space}")]
    public async Task<IActionResult> SubmitStats(string space, CancellationToken ct)
    {
        var res = await api.PostAsync(
            $"/jobs/stats/{Uri.EscapeDataString(space)}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>List all jobs.</summary>
    [HttpGet("")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var res = await api.GetAsync("/jobs", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Show details of a job.</summary>
    [HttpGet("{jobId:int}")]
    public async Task<IActionResult> Show(int jobId, CancellationToken ct)
    {
        var res = await api.GetAsync($"/jobs/{jobId}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Stop a running job.</summary>
    [HttpPost("{jobId:int}/stop")]
    public async Task<IActionResult> Stop(int jobId, CancellationToken ct)
    {
        var res = await api.PostAsync($"/jobs/{jobId}/stop", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Recover finished jobs.</summary>
    [HttpPost("recover")]
    public async Task<IActionResult> Recover(CancellationToken ct)
    {
        var res = await api.PostAsync("/jobs/recover", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}