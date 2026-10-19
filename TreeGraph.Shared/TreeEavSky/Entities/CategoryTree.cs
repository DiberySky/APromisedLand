using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using TreeGraph.Shared.NodeEavSky.Abstractions;
using TreeGraph.Shared.TreeEavSky.Abstractions;

namespace TreeGraph.Shared.TreeEavSky.Entities;

/// <summary>
/// 分类树节点 - 实现完整的树节点接口
/// </summary>
public sealed class CategoryTree : ITreeNodeBase<CategoryTree>,
    IEquatable<CategoryTree>, Inode
{
    [Key]
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(300)]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>节点描述</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>是否启用</summary>
    public bool CanHaveChildren { get; set; } = false;

    /// <summary>是否已归档</summary>
    public bool IsArchived { get; set; } = false;

    /// <summary>排序序号，数值越小排序越靠前</summary>
    public int SortOrder { get; set; } = 0;

    [MaxLength(36)]
    public string? ParentId { get; set; }

    // /// <summary>节点深度（从0开始，根节点为0）</summary>
    // [NotMapped]
    // public int Depth { get; private set; }

    [JsonIgnore]
    [NotMapped]
    public bool HasChildren { get; set; }

    // 导航属性
    [ForeignKey(nameof(Parent))]
    [JsonIgnore]
    [NotMapped]
    public CategoryTree? Parent { get; set; }
    
    // [JsonIgnore]
    // [NotMapped]
    // public ICollection<CategoryTree>? Children { get; set; }

    public string Text() => Name;

    /// <summary>
    /// 计算节点深度
    /// </summary>
    // public void CalculateDepth(int parentDepth = -1)
    // {
    //     Depth = parentDepth + 1;
    //     if (Children?.Any() == true)
    //     {
    //         foreach (var child in Children)
    //         {
    //             child.CalculateDepth(Depth);
    //         }
    //     }
    // }

    public override bool Equals(object? obj)
    {
        if (obj == null) return false;
        return Equals(obj as CategoryTree);
    }

    public bool Equals(CategoryTree? other)
    {
        if (other == null) return false;
        return Id == other.Id;
    }

    public override int GetHashCode()
    {
        return string.IsNullOrEmpty(Id) ? 0 : Id.GetHashCode();
    }

    // /// <summary>
    // /// 运行时使用的示例数据（包含树结构构建）
    // /// </summary>
    // public static List<CategoryTree> SampleData()
    // {
    //     var items = SeedData();
    //
    //     // 构建树结构并计算深度
    //     var lookup = items.ToLookup(i => i.ParentId);
    //     foreach (var root in lookup[null])
    //     {
    //         BuildTree(root, lookup);
    //     }
    //
    //     return items;
    // }

    /// <summary>
    /// EF Core 种子数据：仅根节点「项目类型」。
    /// 空树时前端 TreeEavSky 会无限 loading，必须有一个根节点兜底。
    /// </summary>
    public static List<CategoryTree> SeedData() => new()
    {
        new()
        {
            Id = "d0000000-0000-4000-8000-000000000001",
            Name = "项目类型",
            Description = "根分类",
            CanHaveChildren = true,
            IsArchived = false,
            SortOrder = 0,
            ParentId = null,
            HasChildren = false,
        },
    };
}
