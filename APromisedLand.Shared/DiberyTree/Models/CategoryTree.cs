using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Shared.DiberyTree.Models;

public sealed class CategoryTree : ITreeNode, IEquatable<CategoryTree>
{
    [Key] 
    [MaxLength(36)] 
    public string Id { get; init; } = Guid.NewGuid().ToString();   // 改为 init

    [MaxLength(300)] 
    [Required] 
    public string Name { get; set; } = string.Empty;

    [MaxLength(36)] 
    public string? ParentId { get; set; }

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
        // 去掉冗余的 == true，直接返回 Id 的哈希（若为空则返回 0）
        return string.IsNullOrEmpty(Id) ? 0 : Id.GetHashCode();
    }
    
    public static List<CategoryTree> SampleData()
    {
        return
        [
            new CategoryTree
            {
                Id = "55705350-7071-43A4-AFAF-2F30B3CE2718",
                Name = "Sample Root",
                ParentId = null,
                HasChildren = false,
            },
            new CategoryTree
            {
                Id = "39EA6315-0A74-40F6-A096-8E15CCC98579",
                Name = "Sample 1",
                ParentId = "55705350-7071-43A4-AFAF-2F30B3CE2718",
                HasChildren = false,
            },
            new CategoryTree
            {
                Id = "C1EBEE10-97F6-44C8-9852-2F574515BF51",
                Name = "Sample 1.1",
                ParentId = "39EA6315-0A74-40F6-A096-8E15CCC98579",
                HasChildren = false,
            },
            new CategoryTree
            {
                Id = "C8969ED0-C018-4FDC-AE55-C363BD95C853",
                Name = "Sample 2",
                ParentId = "55705350-7071-43A4-AFAF-2F30B3CE2718",
                HasChildren = false,
            },
            new CategoryTree
            {
                Id = "ED0990CF-5BB7-4C36-A3BB-3AF606AD1974",
                Name = "Sample 2.1",
                ParentId = "C8969ED0-C018-4FDC-AE55-C363BD95C853",
                HasChildren = false,
            },
            new CategoryTree
            {
                Id = "975599CC-B967-4AD2-B4B8-9E00D889FB4D",
                Name = "Sample 2.2",
                ParentId = "C8969ED0-C018-4FDC-AE55-C363BD95C853",
                HasChildren = false,
            },
            new CategoryTree
            {
                Id = "27EE32B0-0F30-4331-AA85-61457B7A0912",
                Name = "Sample 3",
                ParentId = "55705350-7071-43A4-AFAF-2F30B3CE2718",
                HasChildren = false,
            },
            // 删除下面重复的 Sample 3
            new CategoryTree
            {
                Id = "8E971C0E-B99A-4931-AFD6-46E44D6ECE5A",
                Name = "Sample 3.1",
                ParentId = "27EE32B0-0F30-4331-AA85-61457B7A0912",
                HasChildren = false,
            },
            new CategoryTree
            {
                Id = "5C30BACA-3C11-4677-8123-8EC2BE729667",
                Name = "Sample 3.2",
                ParentId = "27EE32B0-0F30-4331-AA85-61457B7A0912",
                HasChildren = false,
            },
            new CategoryTree
            {
                Id = "1B16336D-FB7F-42AA-AFD2-F78388883336",
                Name = "Sample 3.3",
                ParentId = "27EE32B0-0F30-4331-AA85-61457B7A0912",
                HasChildren = false,
            },
            new CategoryTree
            {
                Id = "35F02829-6490-467E-9D3E-C2EBF0EAA2B4",
                Name = "Sample 3.3.1",
                ParentId = "1B16336D-FB7F-42AA-AFD2-F78388883336",
                HasChildren = false,
            },
        ];
    }
}