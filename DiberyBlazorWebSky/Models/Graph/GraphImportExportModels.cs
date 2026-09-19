namespace DiberyBlazorWebSky.Models.Graph;

/// <summary>图导出/导入的 DTO。节点和边均使用 name 引用，便于跨环境迁移。</summary>
public class GraphExportDto
{
    public string GraphName { get; set; } = "";
    public DateTime ExportedAt { get; set; }
    public List<ExportNodeDto> Nodes { get; set; } = new();
    public List<ExportEdgeDto> Edges { get; set; } = new();
}

public class ExportNodeDto
{
    public string Name { get; set; } = "";
    public List<string>? Labels { get; set; }
}

public class ExportEdgeDto
{
    /// <summary>源节点名</summary>
    public string From { get; set; } = "";
    /// <summary>目标节点名</summary>
    public string To { get; set; } = "";
    public string Name { get; set; } = "";
}

/// <summary>导入结果统计。</summary>
public class ImportResult
{
    public int NodesCreated { get; set; }
    public int NodesSkipped { get; set; }
    public int EdgesCreated { get; set; }
    public int EdgesSkipped { get; set; }
    public List<string> Errors { get; set; } = new();
}