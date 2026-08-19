using Microsoft.AspNetCore.Mvc;
using NebulaApi.Proxy.Dtos;
using NebulaApi.Proxy.Services;

namespace NebulaApi.Proxy.Controllers;

/// <summary>
/// Mirrors <c>app.routers.vertices</c>: insert / fetch / upsert /
/// update / delete vertices. All endpoints take the space from the
/// URL and override the body's <c>space</c> field.
/// </summary>
[ApiController]
public sealed class VerticesController : ControllerBase
{
    private readonly NebulaFastApiService _api;
    public VerticesController(NebulaFastApiService api) => _api = api;

    private string BasePath(string space)
        => $"/spaces/{Uri.EscapeDataString(space)}/vertices";

    [HttpPost("/spaces/{space}/vertices")]
    public async Task<IActionResult> Insert(
        string space, [FromBody] VertexInsertIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(BasePath(space), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/vertices/fetch")]
    public async Task<IActionResult> Fetch(
        string space, [FromBody] VertexFetchIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(BasePath(space) + "/fetch", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/vertices/upsert")]
    public async Task<IActionResult> Upsert(
        string space, [FromBody] VertexUpsertIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(BasePath(space) + "/upsert", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/vertices/update")]
    public async Task<IActionResult> Update(
        string space, [FromBody] VertexUpdateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(BasePath(space) + "/update", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("/spaces/{space}/vertices")]
    public async Task<IActionResult> Delete(
        string space, [FromBody] VertexDeleteIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.DeleteAsync(BasePath(space), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}
