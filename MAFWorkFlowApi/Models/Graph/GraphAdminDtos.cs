namespace MAFWorkFlowApi.Models.Graph;

public sealed class UpdateGraphRequest
{
    public string Name { get; set; } = "";
    public Dictionary<string, object?>? Data { get; set; }
}

public sealed class UpdateNodeRequest
{
    public string Name { get; set; } = "";
}

public sealed class UpdateEdgeRequest
{
    public string Name { get; set; } = "";
}

public sealed class EnumerateRequest
{
    public int MaxResults { get; set; } = 20;
    public Guid? ContinuationToken { get; set; }
}

public sealed class ImportJsonRequest
{
    public string JsonContent { get; set; } = "";
    public bool ClearExisting { get; set; }
}

public sealed class ImportCsvRequest
{
    public string NodesCsv { get; set; } = "";
    public string? EdgesCsv { get; set; }
    public bool ClearExisting { get; set; }
}

public sealed class ImportAsNewGraphRequest
{
    public string JsonContent { get; set; } = "";
    public string NewGraphName { get; set; } = "";
}

public sealed class ImportAsNewGraphResponse
{
    public Guid? NewGraphGuid { get; set; }
    public ImportResult Result { get; set; } = new();
}

public sealed class ImportResult
{
    public int NodesCreated { get; set; }
    public int NodesSkipped { get; set; }
    public int EdgesCreated { get; set; }
    public int EdgesSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
}

public sealed class CreateVectorRequest
{
    public Guid NodeGuid { get; set; }
    public string Model { get; set; } = "bge-large";
    public List<float> Vector { get; set; } = new();
}

// ══════════════════════════════════════════════════════════
// 导出的 DTO（Data 统一用 object?）
// ══════════════════════════════════════════════════════════

public sealed class GraphExportDto
{
    public string Version { get; set; } = "1.0";
    public string GraphName { get; set; } = "";
    public DateTime ExportedAt { get; set; }

    /// <summary>★ 类型是 object?，兼容 SDK 的 Graph.Data</summary>
    public object? GraphData { get; set; }

    public List<string>? GraphLabels { get; set; }

    /// <summary>★ 类型是 Dictionary<string, string>（key + value 都非 null）</summary>
    public Dictionary<string, string>? GraphTags { get; set; }

    public List<ExportNodeDto> Nodes { get; set; } = new();
    public List<ExportEdgeDto> Edges { get; set; } = new();
}

public sealed class ExportNodeDto
{
    public string Name { get; set; } = "";
    public List<string>? Labels { get; set; }

    /// <summary>★ 类型是 object?</summary>
    public object? Data { get; set; }
}

public sealed class ExportEdgeDto
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>★ 类型是 object?</summary>
    public object? Data { get; set; }
}