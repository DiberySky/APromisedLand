using APromisedLand.Api.NebulaGraph.Dtos;
using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.NebulaGraph.ControllerBases;

/// <summary>
/// Mirrors <c>app.routers.edges</c>: insert / fetch / upsert /
/// update / delete edges. All endpoints take the space from the URL
/// and override the body's <c>space</c> field.
/// </summary>
[ApiController]
[Route("NebulaGraph/spaces/{space}/edges")]
public abstract class EdgesControllerBase(NebulaFastApiService api) : ControllerBase
{
    [HttpPost("")]
    public async Task<IActionResult> Insert(
        string space, [FromBody] EdgeInsertIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/edges", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("fetch")]
    public async Task<IActionResult> Fetch(
        string space, [FromBody] EdgeFetchIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/edges/fetch", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert(
        string space, [FromBody] EdgeUpsertIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/edges/upsert", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update(
        string space, [FromBody] EdgeUpdateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/edges/update", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("")]
    public async Task<IActionResult> Delete(
        string space, [FromBody] EdgeDeleteIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.DeleteAsync($"/spaces/{Uri.EscapeDataString(space)}/edges", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}