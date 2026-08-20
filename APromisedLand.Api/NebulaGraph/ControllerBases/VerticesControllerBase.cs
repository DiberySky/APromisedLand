using APromisedLand.Api.NebulaGraph.Dtos;
using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.NebulaGraph.ControllerBases;

/// <summary>
/// Mirrors <c>app.routers.vertices</c>: insert / fetch / upsert /
/// update / delete vertices. All endpoints take the space from the
/// URL and override the body's <c>space</c> field.
/// </summary>
[ApiController]
[Route("NebulaGraph/spaces/{space}/vertices")]
public abstract class VerticesControllerBase(NebulaFastApiService api) : ControllerBase
{
    [HttpPost("")]
    public async Task<IActionResult> Insert(
        string space, [FromBody] VertexInsertIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/vertices", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("fetch")]
    public async Task<IActionResult> Fetch(
        string space, [FromBody] VertexFetchIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/vertices/fetch", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert(
        string space, [FromBody] VertexUpsertIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/vertices/upsert", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update(
        string space, [FromBody] VertexUpdateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/vertices/update", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("")]
    public async Task<IActionResult> Delete(
        string space, [FromBody] VertexDeleteIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.DeleteAsync($"/spaces/{Uri.EscapeDataString(space)}/vertices", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}