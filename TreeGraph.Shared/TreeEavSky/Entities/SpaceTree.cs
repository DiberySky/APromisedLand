using System.Text.Json.Serialization;
using TreeGraph.Shared.TreeEavSky.Abstractions;

namespace TreeGraph.Shared.TreeEavSky.Entities;

/// <summary>
/// 空间树节点（克隆自 UnitTree，不播种，表 space_trees 初始为空）。
/// </summary>
public class SpaceTree : ITreeNodeBase<SpaceTree>
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;

    public string? ParentId { get; set; }
    public string? Description { get; set; }
    public bool CanHaveChildren { get; set; }
    public int SortOrder { get; set; }
    public bool HasChildren { get; set; }

    /// <summary>父节点导航属性；JSON 忽略，避免 TreeNodeDto.Value 序列化时循环引用。</summary>
    [JsonIgnore]
    public SpaceTree? Parent { get; set; }

    public string Text()
    {
        return Name;
    }

    public static List<SpaceTree> SeedData()
    {
        return
        [
            // 唯一根节点（固定 GUID），其余节点全部由界面创建
            new SpaceTree
            {
                Id = "EBBAD64B-2119-47EB-95C3-01D3DD796774",
                Name = "空间树",
                Description = "",
                ParentId = null,
                CanHaveChildren = true,
                SortOrder = 0,
                HasChildren = false
            }
        ];
    }
}
