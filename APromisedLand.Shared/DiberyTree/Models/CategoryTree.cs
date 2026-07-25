using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Shared.DiberyTree.Models;

/// <summary>
/// 分类树节点 - 实现完整的树节点接口
/// </summary>
public sealed class CategoryTree : ITreeNodeBase, IArchivableTreeNodeBase, IHierarchyTreeNodeBase, IEquatable<CategoryTree>
{
    [Key]
    [MaxLength(36)]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [MaxLength(300)]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>节点描述</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>是否已归档</summary>
    public bool IsArchived { get; set; } = false;

    /// <summary>排序序号，数值越小排序越靠前</summary>
    public int SortOrder { get; set; } = 0;

    [MaxLength(36)]
    public string? ParentId { get; set; }

    /// <summary>节点深度（从0开始，根节点为0）</summary>
    [NotMapped]
    public int Depth { get; private set; }

    [JsonIgnore]
    [NotMapped]
    public bool HasChildren { get; set; }

    // 导航属性
    [ForeignKey(nameof(ParentId))]
    [JsonIgnore]
    [NotMapped]
    public CategoryTree? Parent { get; set; }

    [JsonIgnore]
    [NotMapped]
    public ICollection<CategoryTree>? Children { get; set; }

    public string Text() => Name;

    /// <summary>
    /// 计算节点深度
    /// </summary>
    public void CalculateDepth(int parentDepth = -1)
    {
        Depth = parentDepth + 1;
        if (Children?.Any() == true)
        {
            foreach (var child in Children)
            {
                child.CalculateDepth(Depth);
            }
        }
    }

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

    /// <summary>
    /// 运行时使用的示例数据（包含树结构构建）
    /// </summary>
    public static List<CategoryTree> SampleData()
    {
        var items = SeedData();

        // 构建树结构并计算深度
        var lookup = items.ToLookup(i => i.ParentId);
        foreach (var root in lookup[null])
        {
            BuildTree(root, lookup);
        }

        return items;
    }

    /// <summary>
    /// EF Core 种子数据使用的纯扁平数据（不包含导航属性）
    /// </summary>
    public static List<CategoryTree> SeedData()
    {
        return new List<CategoryTree>
        {
            new()
            {
                Id = "55705350-7071-43A4-AFAF-2F30B3CE2718",
                Name = "Sample Root",
                Description = "根分类示例",
                IsActive = true,
                IsArchived = false,
                SortOrder = 0,
                ParentId = null,
                HasChildren = true,
            },
            new()
            {
                Id = "39EA6315-0A74-40F6-A096-8E15CCC98579",
                Name = "Sample 1",
                Description = "子分类 1",
                IsActive = true,
                IsArchived = false,
                SortOrder = 1,
                ParentId = "55705350-7071-43A4-AFAF-2F30B3CE2718",
                HasChildren = true,
            },
            new()
            {
                Id = "C1EBEE10-97F6-44C8-9852-2F574515BF51",
                Name = "Sample 1.1",
                Description = "子分类 1.1",
                IsActive = true,
                IsArchived = false,
                SortOrder = 0,
                ParentId = "39EA6315-0A74-40F6-A096-8E15CCC98579",
                HasChildren = false,
            },
            new()
            {
                Id = "C8969ED0-C018-4FDC-AE55-C363BD95C853",
                Name = "Sample 2",
                Description = "子分类 2",
                IsActive = true,
                IsArchived = false,
                SortOrder = 2,
                ParentId = "55705350-7071-43A4-AFAF-2F30B3CE2718",
                HasChildren = true,
            },
            new()
            {
                Id = "ED0990CF-5BB7-4C36-A3BB-3AF606AD1974",
                Name = "Sample 2.1",
                Description = "子分类 2.1",
                IsActive = true,
                IsArchived = false,
                SortOrder = 0,
                ParentId = "C8969ED0-C018-4FDC-AE55-C363BD95C853",
                HasChildren = false,
            },
            new()
            {
                Id = "975599CC-B967-4AD2-B4B8-9E00D889FB4D",
                Name = "Sample 2.2",
                Description = "子分类 2.2 [Archived]",
                IsActive = false,
                IsArchived = true,
                SortOrder = 1,
                ParentId = "C8969ED0-C018-4FDC-AE55-C363BD95C853",
                HasChildren = false,
            },
            new()
            {
                Id = "27EE32B0-0F30-4331-AA85-61457B7A0912",
                Name = "Sample 3",
                Description = "子分类 3",
                IsActive = true,
                IsArchived = false,
                SortOrder = 3,
                ParentId = "55705350-7071-43A4-AFAF-2F30B3CE2718",
                HasChildren = true,
            },
            new()
            {
                Id = "8E971C0E-B99A-4931-AFD6-46E44D6ECE5A",
                Name = "Sample 3.1",
                Description = "子分类 3.1",
                IsActive = true,
                IsArchived = false,
                SortOrder = 0,
                ParentId = "27EE32B0-0F30-4331-AA85-61457B7A0912",
                HasChildren = false,
            },
            new()
            {
                Id = "5C30BACA-3C11-4677-8123-8EC2BE729667",
                Name = "Sample 3.2",
                Description = "子分类 3.2",
                IsActive = true,
                IsArchived = false,
                SortOrder = 1,
                ParentId = "27EE32B0-0F30-4331-AA85-61457B7A0912",
                HasChildren = false,
            },
            new()
            {
                Id = "1B16336D-FB7F-42AA-AFD2-F78388883336",
                Name = "Sample 3.3",
                Description = "子分类 3.3",
                IsActive = true,
                IsArchived = false,
                SortOrder = 2,
                ParentId = "27EE32B0-0F30-4331-AA85-61457B7A0912",
                HasChildren = true,
            },
            new()
            {
                Id = "35F02829-6490-467E-9D3E-C2EBF0EAA2B4",
                Name = "Sample 3.3.1",
                Description = "子分类 3.3.1",
                IsActive = true,
                IsArchived = false,
                SortOrder = 0,
                ParentId = "1B16336D-FB7F-42AA-AFD2-F78388883336",
                HasChildren = false,
            },
        };
    }

    private static void BuildTree(CategoryTree node, ILookup<string?, CategoryTree> lookup)
    {
        node.Children = lookup[node.Id].ToList();
        node.HasChildren = node.Children.Any();
        foreach (var child in node.Children)
        {
            child.Parent = node;
            BuildTree(child, lookup);
        }
        node.CalculateDepth();
    }
}
