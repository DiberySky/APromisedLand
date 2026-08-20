using APromisedLand.Api.NebulaGraph.Dtos;
using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.NebulaGraph.ControllerBases;

/// <summary>
/// Mirrors <c>app.routers.schema</c>: tag / edge / index / fulltext
/// index management. All endpoints use the
/// <c>/spaces/{space}/...</c> path; the body's <c>space</c> field is
/// always overridden by the route parameter (matching the FastAPI
/// <c>model_copy(update={"space": space})</c> behaviour).
/// </summary>
[ApiController]
[Route("NebulaGraph/spaces/{space}")]
public abstract class SchemaControllerBase(NebulaFastApiService api) : ControllerBase
{
    // ----------------------------------------------------------------- //
    // Tags
    // ----------------------------------------------------------------- //

    [HttpGet("tags")]
    public async Task<IActionResult> ListTags(string space, CancellationToken ct)
    {
        var res = await api.GetAsync($"/spaces/{Uri.EscapeDataString(space)}/tags", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("tags")]
    public async Task<IActionResult> CreateTag(
        string space, [FromBody] TagCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/tags", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("tags/{name}")]
    public async Task<IActionResult> DescribeTag(string space, string name, CancellationToken ct)
    {
        var res = await api.GetAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/tags/{Uri.EscapeDataString(name)}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("tags/{name}/ddl")]
    public async Task<IActionResult> ShowCreateTag(string space, string name, CancellationToken ct)
    {
        var res = await api.GetAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/tags/{Uri.EscapeDataString(name)}/ddl", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPut("tags/{name}")]
    public async Task<IActionResult> AlterTag(
        string space, string name, [FromBody] AlterSchemaIn body, CancellationToken ct)
    {
        body.Space = space;
        body.Name = name;
        body.Kind = "tag";
        var res = await api.PutAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/tags/{Uri.EscapeDataString(name)}", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("tags/{name}")]
    public async Task<IActionResult> DropTag(
        string space, string name, [FromQuery] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await api.DeleteAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/tags/{Uri.EscapeDataString(name)}",
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    // ----------------------------------------------------------------- //
    // Edges (schema)
    // ----------------------------------------------------------------- //

    [HttpGet("edges")]
    public async Task<IActionResult> ListEdges(string space, CancellationToken ct)
    {
        var res = await api.GetAsync($"/spaces/{Uri.EscapeDataString(space)}/edges", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("edges")]
    public async Task<IActionResult> CreateEdge(
        string space, [FromBody] EdgeCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/edges", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("edges/{name}")]
    public async Task<IActionResult> DescribeEdge(string space, string name, CancellationToken ct)
    {
        var res = await api.GetAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/edges/{Uri.EscapeDataString(name)}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("edges/{name}/ddl")]
    public async Task<IActionResult> ShowCreateEdge(string space, string name, CancellationToken ct)
    {
        var res = await api.GetAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/edges/{Uri.EscapeDataString(name)}/ddl", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPut("edges/{name}")]
    public async Task<IActionResult> AlterEdge(
        string space, string name, [FromBody] AlterSchemaIn body, CancellationToken ct)
    {
        body.Space = space;
        body.Name = name;
        body.Kind = "edge";
        var res = await api.PutAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/edges/{Uri.EscapeDataString(name)}", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("edges/{name}")]
    public async Task<IActionResult> DropEdge(
        string space, string name, [FromQuery] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await api.DeleteAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/edges/{Uri.EscapeDataString(name)}",
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    // ----------------------------------------------------------------- //
    // Indexes (tag / edge)
    // ----------------------------------------------------------------- //

    [HttpGet("indexes")]
    public async Task<IActionResult> ListIndexes(string space, CancellationToken ct)
    {
        var res = await api.GetAsync($"/spaces/{Uri.EscapeDataString(space)}/indexes", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("indexes/tag")]
    public async Task<IActionResult> ListTagIndexes(string space, CancellationToken ct)
    {
        var res = await api.GetAsync($"/spaces/{Uri.EscapeDataString(space)}/indexes/tag", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("indexes/edge")]
    public async Task<IActionResult> ListEdgeIndexes(string space, CancellationToken ct)
    {
        var res = await api.GetAsync($"/spaces/{Uri.EscapeDataString(space)}/indexes/edge", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("indexes/tag")]
    public async Task<IActionResult> CreateTagIndex(
        string space, [FromBody] TagIndexCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/indexes/tag", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("indexes/edge")]
    public async Task<IActionResult> CreateEdgeIndex(
        string space, [FromBody] EdgeIndexCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/indexes/edge", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("indexes/tag/{name}")]
    public async Task<IActionResult> DescribeTagIndex(
        string space, string name, CancellationToken ct)
    {
        var res = await api.GetAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/indexes/tag/{Uri.EscapeDataString(name)}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpGet("indexes/edge/{name}")]
    public async Task<IActionResult> DescribeEdgeIndex(
        string space, string name, CancellationToken ct)
    {
        var res = await api.GetAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/indexes/edge/{Uri.EscapeDataString(name)}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("indexes/{name}/rebuild")]
    public async Task<IActionResult> RebuildIndex(
        string space, string name, [FromQuery] string kind = "tag", CancellationToken ct = default)
    {
        var res = await api.PostAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/indexes/{Uri.EscapeDataString(name)}/rebuild",
            new[] { new KeyValuePair<string, string?>("kind", kind) },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("indexes/{name}")]
    public async Task<IActionResult> DropIndex(
        string space, string name, [FromQuery] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await api.DeleteAsync(
            $"/spaces/{Uri.EscapeDataString(space)}/indexes/{Uri.EscapeDataString(name)}",
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    // ----------------------------------------------------------------- //
    // Fulltext indexes
    // ----------------------------------------------------------------- //

    // 注意：ListFulltextIndexes 和 DropFulltextIndex 原本不属于 /spaces/{space} 层级，
    // 这里使用绝对路径并加上 /NebulaGraph 前缀以保持统一。
    [HttpGet("/NebulaGraph/fulltext-indexes")]
    public async Task<IActionResult> ListFulltextIndexes(CancellationToken ct)
    {
        var res = await api.GetAsync("/fulltext-indexes", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpPost("fulltext-indexes")]
    public async Task<IActionResult> CreateFulltextIndex(
        string space, [FromBody] FulltextIndexCreateIn body, CancellationToken ct)
    {
        body.Space = space;
        var res = await api.PostAsync($"/spaces/{Uri.EscapeDataString(space)}/fulltext-indexes", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    [HttpDelete("/NebulaGraph/fulltext-indexes/{name}")]
    public async Task<IActionResult> DropFulltextIndex(
        string name, [FromQuery] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await api.DeleteAsync(
            $"/fulltext-indexes/{Uri.EscapeDataString(name)}",
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}