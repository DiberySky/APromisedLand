namespace TreeGraphApi.Data.Seeding;

/// <summary>
/// 声明式种子节点。Id 默认留空,由 TreeSeeder 自动生成 GUID。
/// 若需要稳定 Id,可在数据中显式指定。
/// </summary>
public class SeedNode
{
    public string? Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string NodeType { get; set; } = "folder";
    public int SortOrder { get; set; }
    public List<SeedNode> Children { get; set; } = new();
}
