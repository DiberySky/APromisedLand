using DiberyBlazorWebSky.Models.Graph;

namespace DiberyBlazorWebSky.Services;

/// <summary>
/// 图数据的批量导入/导出。
/// 
/// ★ 本类已收敛为 GraphAdminApiClient 的薄封装 ——
///   所有 LiteGraph 直连逻辑（CSV 解析、节点去重、边映射等）
///   都在 MAFWorkFlowApi 的 GraphExportService 里实现，
///   Blazor 侧仅负责调用 HTTP 端点 + 结果转换。
/// </summary>
public sealed class GraphImportExportService
{
    private readonly GraphAdminApiClient _admin;
    private readonly ILogger<GraphImportExportService> _logger;

    public GraphImportExportService(
        GraphAdminApiClient admin,
        ILogger<GraphImportExportService> logger)
    {
        _admin = admin;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════
    // 导出
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 导出当前图为 JSON 字符串。
    /// <paramref name="graphName"/> 已废弃（服务端从图元数据里取），
    /// 保留仅为兼容旧调用方。
    /// </summary>
    public Task<string?> ExportToJsonAsync(Guid graphGuid, string? graphName = null)
        => _admin.ExportGraphJsonAsync(graphGuid);

    /// <summary>导出节点为 CSV（name,labels）。</summary>
    public Task<string?> ExportNodesToCsvAsync(Guid graphGuid)
        => _admin.ExportNodesCsvAsync(graphGuid);

    /// <summary>导出边为 CSV（from,to,name）。</summary>
    public Task<string?> ExportEdgesToCsvAsync(Guid graphGuid)
        => _admin.ExportEdgesCsvAsync(graphGuid);

    // ══════════════════════════════════════════════════════
    // 导入
    // ══════════════════════════════════════════════════════

    /// <summary>从 JSON 导入。</summary>
    public async Task<ImportResult> ImportFromJsonAsync(
        Guid graphGuid, string jsonContent, bool clearExisting)
    {
        var dto = await _admin.ImportJsonAsync(graphGuid, jsonContent, clearExisting);
        return ToLocalResult(dto);
    }

    /// <summary>从 CSV 导入（节点 CSV + 可选边 CSV）。</summary>
    public async Task<ImportResult> ImportFromCsvAsync(
        Guid graphGuid, string nodesCsv, string? edgesCsv, bool clearExisting)
    {
        var dto = await _admin.ImportCsvAsync(graphGuid, nodesCsv, edgesCsv, clearExisting);
        return ToLocalResult(dto);
    }

    /// <summary>把 JSON 导入为新图。返回新图 GUID 及导入结果。</summary>
    public async Task<(Guid? NewGraphGuid, ImportResult Result)> ImportAsNewGraphAsync(
        string jsonContent, string newGraphName)
    {
        var resp = await _admin.ImportAsNewGraphAsync(jsonContent, newGraphName);
        return (resp.NewGraphGuid, ToLocalResult(resp.Result));
    }

    // ══════════════════════════════════════════════════════
    // 内部
    // ══════════════════════════════════════════════════════

    private ImportResult ToLocalResult(ImportResultDto dto) => new()
    {
        NodesCreated = dto.NodesCreated,
        NodesSkipped = dto.NodesSkipped,
        EdgesCreated = dto.EdgesCreated,
        EdgesSkipped = dto.EdgesSkipped,
        Errors       = dto.Errors ?? new List<string>()
    };
}