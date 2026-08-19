using Microsoft.AspNetCore.Mvc;
using NebulaApi.Proxy.Dtos;
using NebulaApi.Proxy.Services;

namespace NebulaApi.Proxy.Controllers;

/// <summary>
/// Mirrors <c>app.routers.schema</c>: tag / edge / index / fulltext
/// index management. All endpoints use the
/// <c>/spaces/{space}/...</c> path; the body's <c>space</c> field is
/// always overridden by the route parameter (matching the FastAPI
/// <c>model_copy(update={"space": space})</c> behaviour).
/// </summary>
[ApiController]
public sealed class SchemaController : ControllerBase
{
    private readonly NebulaFastApiService _api;
    public SchemaController(NebulaFastApiService api) => _api = api;

    private string SpacePath(string space, string suffix = "")
        => $"/spaces/{Uri.EscapeDataString(space)}{suffix}";

    // ----------------------------------------------------------------- //
    // Tags
    // ----------------------------------------------------------------- //

    [HttpGet("/spaces/{space}/tags")]
    public async Task<IActionResult> ListTags(string space, CancellationToken ct)
    {
        var res = await _api.GetAsync(SpacePath(space, "/tags"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/tags")]
    public async Task<IActionResult> CreateTag(
        string space, [FromBody] TagCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(SpacePath(space, "/tags"), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("/spaces/{space}/tags/{name}")]
    public async Task<IActionResult> DescribeTag(string space, string name, CancellationToken ct)
    {
        var res = await _api.GetAsync(
            SpacePath(space, $"/tags/{Uri.EscapeDataString(name)}"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("/spaces/{space}/tags/{name}/ddl")]
    public async Task<IActionResult> ShowCreateTag(string space, string name, CancellationToken ct)
    {
        var res = await _api.GetAsync(
            SpacePath(space, $"/tags/{Uri.EscapeDataString(name)}/ddl"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPut("/spaces/{space}/tags/{name}")]
    public async Task<IActionResult> AlterTag(
        string space, string name, [FromBody] AlterSchemaIn body, CancellationToken ct)
    {
        body.Space = space;
        body.Name = name;
        body.Kind = "tag";
        var res = await _api.PutAsync(
            SpacePath(space, $"/tags/{Uri.EscapeDataString(name)}"), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("/spaces/{space}/tags/{name}")]
    public async Task<IActionResult> DropTag(
        string space, string name, [FromQuery] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(
            SpacePath(space, $"/tags/{Uri.EscapeDataString(name)}"),
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    // ----------------------------------------------------------------- //
    // Edges
    // ----------------------------------------------------------------- //

    [HttpGet("/spaces/{space}/edges")]
    public async Task<IActionResult> ListEdges(string space, CancellationToken ct)
    {
        var res = await _api.GetAsync(SpacePath(space, "/edges"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/edges")]
    public async Task<IActionResult> CreateEdge(
        string space, [FromBody] EdgeCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(SpacePath(space, "/edges"), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("/spaces/{space}/edges/{name}")]
    public async Task<IActionResult> DescribeEdge(string space, string name, CancellationToken ct)
    {
        var res = await _api.GetAsync(
            SpacePath(space, $"/edges/{Uri.EscapeDataString(name)}"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("/spaces/{space}/edges/{name}/ddl")]
    public async Task<IActionResult> ShowCreateEdge(string space, string name, CancellationToken ct)
    {
        var res = await _api.GetAsync(
            SpacePath(space, $"/edges/{Uri.EscapeDataString(name)}/ddl"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPut("/spaces/{space}/edges/{name}")]
    public async Task<IActionResult> AlterEdge(
        string space, string name, [FromBody] AlterSchemaIn body, CancellationToken ct)
    {
        body.Space = space;
        body.Name = name;
        body.Kind = "edge";
        var res = await _api.PutAsync(
            SpacePath(space, $"/edges/{Uri.EscapeDataString(name)}"), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("/spaces/{space}/edges/{name}")]
    public async Task<IActionResult> DropEdge(
        string space, string name, [FromQuery] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(
            SpacePath(space, $"/edges/{Uri.EscapeDataString(name)}"),
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    // ----------------------------------------------------------------- //
    // Indexes (tag / edge)
    // ----------------------------------------------------------------- //

    [HttpGet("/spaces/{space}/indexes")]
    public async Task<IActionResult> ListIndexes(string space, CancellationToken ct)
    {
        var res = await _api.GetAsync(SpacePath(space, "/indexes"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("/spaces/{space}/indexes/tag")]
    public async Task<IActionResult> ListTagIndexes(string space, CancellationToken ct)
    {
        var res = await _api.GetAsync(SpacePath(space, "/indexes/tag"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("/spaces/{space}/indexes/edge")]
    public async Task<IActionResult> ListEdgeIndexes(string space, CancellationToken ct)
    {
        var res = await _api.GetAsync(SpacePath(space, "/indexes/edge"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/indexes/tag")]
    public async Task<IActionResult> CreateTagIndex(
        string space, [FromBody] TagIndexCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(SpacePath(space, "/indexes/tag"), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/indexes/edge")]
    public async Task<IActionResult> CreateEdgeIndex(
        string space, [FromBody] EdgeIndexCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(SpacePath(space, "/indexes/edge"), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("/spaces/{space}/indexes/tag/{name}")]
    public async Task<IActionResult> DescribeTagIndex(
        string space, string name, CancellationToken ct)
    {
        var res = await _api.GetAsync(
            SpacePath(space, $"/indexes/tag/{Uri.EscapeDataString(name)}"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("/spaces/{space}/indexes/edge/{name}")]
    public async Task<IActionResult> DescribeEdgeIndex(
        string space, string name, CancellationToken ct)
    {
        var res = await _api.GetAsync(
            SpacePath(space, $"/indexes/edge/{Uri.EscapeDataString(name)}"), ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/indexes/{name}/rebuild")]
    public async Task<IActionResult> RebuildIndex(
        string space, string name, [FromQuery] string kind = "tag", CancellationToken ct = default)
    {
        var res = await _api.PostAsync(
            SpacePath(space, $"/indexes/{Uri.EscapeDataString(name)}/rebuild"),
            new[] { new KeyValuePair<string, string?>("kind", kind) },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("/spaces/{space}/indexes/{name}")]
    public async Task<IActionResult> DropIndex(
        string space, string name, [FromQuery] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(
            SpacePath(space, $"/indexes/{Uri.EscapeDataString(name)}"),
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    // ----------------------------------------------------------------- //
    // Fulltext indexes
    // ----------------------------------------------------------------- //

    [HttpGet("/fulltext-indexes")]
    public async Task<IActionResult> ListFulltextIndexes(CancellationToken ct)
    {
        var res = await _api.GetAsync("/fulltext-indexes", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("/spaces/{space}/fulltext-indexes")]
    public async Task<IActionResult> CreateFulltextIndex(
        string space, [FromBody] FulltextIndexCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await _api.PostAsync(SpacePath(space, "/fulltext-indexes"), body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("/fulltext-indexes/{name}")]
    public async Task<IActionResult> DropFulltextIndex(
        string name, [FromQuery] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await _api.DeleteAsync(
            $"/fulltext-indexes/{Uri.EscapeDataString(name)}",
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}
