using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;
using NebulaApi.Proxy.Dtos;

namespace NebulaApi.Proxy.Controllers;

/// <summary>
/// Mirrors <c>app.routers.edges</c>: insert / fetch / upsert /
/// update / delete edges. All endpoints take the space from the URL
/// and override the body's <c>space</c> field.
/// </summary>
[ApiController]
public sealed class EdgesController : ControllerBase
{
    private readonly NebulaFastApiService _api;
    public EdgesController(NebulaFastApiService api) => _api = api;

    private string BasePath(string space)
        => $"/spaces/{Uri.EscapeDataString(space)}/edges";

    [HttpPost("/spaces/{space}/edges")]
    public async Task<IActionResult> Insert(
        string space, [FromBody] EdgeInsertIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(BasePath(space), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/edges/fetch")]
    public async Task<IActionResult> Fetch(
        string space, [FromBody] EdgeFetchIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(BasePath(space) + "/fetch", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/edges/upsert")]
    public async Task<IActionResult> Upsert(
        string space, [FromBody] EdgeUpsertIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(BasePath(space) + "/upsert", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/edges/update")]
    public async Task<IActionResult> Update(
        string space, [FromBody] EdgeUpdateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(BasePath(space) + "/update", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("/spaces/{space}/edges")]
    public async Task<IActionResult> Delete(
        string space, [FromBody] EdgeDeleteIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.DeleteAsync(BasePath(space), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}
