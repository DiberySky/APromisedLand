using MAFWorkFlowApi.Models.Graph;
using MAFWorkFlowApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace MAFWorkFlowApi.Controllers;

/// <summary>
/// 图级导出/导入（跨环境迁移）。
/// 路由前缀：/api/graph-admin
/// </summary>
[ApiController]
[Route("api/graph-admin")]
[Produces("application/json")]
public sealed class GraphAdminController : ControllerBase
{
    private readonly GraphExportService _export;
    private readonly ILogger<GraphAdminController> _logger;

    public GraphAdminController(
        GraphExportService export,
        ILogger<GraphAdminController> logger)
    {
        _export = export;
        _logger = logger;
    }

    // ─── 导出 ─────────────────────────────────────────

    [HttpGet("graphs/{graphGuid}/export/json")]
    [Produces("application/json")]
    public async Task<IActionResult> ExportJson(
        [FromRoute] Guid graphGuid, CancellationToken ct)
    {
        var json = await _export.ExportToJsonAsync(graphGuid, ct);
        return Content(json, "application/json; charset=utf-8");
    }

    [HttpGet("graphs/{graphGuid}/export/nodes.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportNodesCsv(
        [FromRoute] Guid graphGuid, CancellationToken ct)
    {
        var csv = await _export.ExportNodesToCsvAsync(graphGuid, ct);
        return Content(csv, "text/csv; charset=utf-8");
    }

    [HttpGet("graphs/{graphGuid}/export/edges.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportEdgesCsv(
        [FromRoute] Guid graphGuid, CancellationToken ct)
    {
        var csv = await _export.ExportEdgesToCsvAsync(graphGuid, ct);
        return Content(csv, "text/csv; charset=utf-8");
    }

    // ─── 导入 ─────────────────────────────────────────

    [HttpPost("graphs/{graphGuid}/import/json")]
    public async Task<ActionResult<ImportResult>> ImportJson(
        [FromRoute] Guid graphGuid,
        [FromBody] ImportJsonRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("导入 JSON 到图 {Guid}，ClearExisting={Clear}",
            graphGuid, request.ClearExisting);

        var result = await _export.ImportFromJsonAsync(
            graphGuid, request.JsonContent, request.ClearExisting, ct);
        return Ok(result);
    }

    [HttpPost("graphs/{graphGuid}/import/csv")]
    public async Task<ActionResult<ImportResult>> ImportCsv(
        [FromRoute] Guid graphGuid,
        [FromBody] ImportCsvRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("从 CSV 导入到图 {Guid}，ClearExisting={Clear}",
            graphGuid, request.ClearExisting);

        var result = await _export.ImportFromCsvAsync(
            graphGuid, request.NodesCsv, request.EdgesCsv, request.ClearExisting, ct);
        return Ok(result);
    }

    // ─── 导入为新图 ─────────────────────────────────

    [HttpPost("import-as-new-graph")]
    public async Task<ActionResult<ImportAsNewGraphResponse>> ImportAsNewGraph(
        [FromBody] ImportAsNewGraphRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("导入为新图，新图名={Name}", request.NewGraphName);

        var (newGuid, result) = await _export.ImportAsNewGraphAsync(
            request.JsonContent, request.NewGraphName, ct);

        return Ok(new ImportAsNewGraphResponse
        {
            NewGraphGuid = newGuid,
            Result = result
        });
    }
}