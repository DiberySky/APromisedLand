using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using TreeGraph.Shared.NodeSky.Entities;
using TreeGraph.Shared.TreeEavSky.Abstractions;

namespace TreeGraph.Shared.TreeEavSky.Entities;

/// <summary>
/// EAV 树节点（克隆自 SpaceTree，不播种，表 eav_trees 初始为空）。
/// </summary>
public class EavTree : ITreeNodeBase<EavTree>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>共享主键导航：宿主 Id 即 nodes.id，名称唯一真源在 Node.Name。</summary>
    [JsonIgnore]
    public Node? Node { get; set; }

    private string? _inputName;

    /// <summary>名称（非映射列）：运行时读穿 Node.Name；入参绑定时通过 setter 暂存（Create 时写入 nodes）。</summary>
    [NotMapped]
    public string? Name
    {
        get => Node?.Name ?? _inputName;
        set => _inputName = value;
    }

    public string? ParentId { get; set; }
    public string? Description { get; set; }
    public bool CanHaveChildren { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }

    /// <summary>父节点导航属性；JSON 忽略，避免 TreeNodeDto.Value 序列化时循环引用。</summary>
    [JsonIgnore]
    public EavTree? Parent { get; set; }
}
