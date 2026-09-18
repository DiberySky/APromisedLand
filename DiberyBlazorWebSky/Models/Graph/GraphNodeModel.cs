using Blazor.Diagrams.Core.Geometry;
using Blazor.Diagrams.Core.Models;

namespace DiberyBlazorWebSky.Models.Graph;

/// <summary>图表画布中节点的数据模型。</summary>
public class GraphNodeModel : NodeModel
{
    public GraphNodeModel(
        string id,
        string name,
        List<string>? labels,
        Point? position = null)
        : base(id, position)
    {
        Name = name;
        Labels = labels ?? [];
        Size = new Size(180, 60);
    }

    public string Name { get; }
    public List<string> Labels { get; }

    /// <summary>用于在画布上显示的名称。</summary>
    public string DisplayName => Labels.Count > 0
        ? $"{Name} [{string.Join(", ", Labels)}]"
        : Name;
}