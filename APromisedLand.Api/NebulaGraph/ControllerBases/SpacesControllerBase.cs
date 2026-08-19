using APromisedLand.Api.NebulaGraph.Dtos;
using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.NebulaGraph.ControllerBases;

/// <summary>
/// Mirrors <c>app.routers.spaces</c>: CRUD for graph spaces.
/// </summary>
[ApiController]
[Route("spaces")]
public abstract class SpacesControllerBase(NebulaFastApiService api) : ControllerBase
{
    /// <summary>List all graph spaces.</summary>
    [HttpGet("")]
    public async Task<IActionResult> ListSpaces(CancellationToken ct)
    {
        var res = await api.GetAsync("/spaces", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Create a graph space.</summary>
    [HttpPost("")]
    public async Task<IActionResult> CreateSpace(
        [FromBody] SpaceCreateIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/spaces", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Describe a graph space.</summary>
    [HttpGet("{name}")]
    public async Task<IActionResult> Describe(
        string name, CancellationToken ct)
    {
        var res = await api.GetAsync($"/spaces/{Uri.EscapeDataString(name)}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Show CREATE SPACE statement.</summary>
    [HttpGet("{name}/ddl")]
    public async Task<IActionResult> ShowCreate(
        string name, CancellationToken ct)
    {
        var res = await api.GetAsync($"/spaces/{Uri.EscapeDataString(name)}/ddl", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Alter a graph space comment.</summary>
    [HttpPut("{name}/comment")]
    public async Task<IActionResult> AlterComment(
        string name, [FromBody] SpaceAlterCommentIn body, CancellationToken ct)
    {
        var res = await api.PutAsync(
            $"/spaces/{Uri.EscapeDataString(name)}/comment", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Drop a graph space.</summary>
    [HttpDelete("{name}")]
    public async Task<IActionResult> Drop(
        string name, [FromQuery(Name = "if_exists")] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await api.DeleteAsync(
            $"/spaces/{Uri.EscapeDataString(name)}",
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}
