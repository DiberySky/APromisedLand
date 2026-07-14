using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using APromisedLand.Shared.DiberyTree.Interfaces;

namespace APromisedLand.Shared.DiberyTree.Models;

public class Category : ITreeNode
{
    [MaxLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [MaxLength(300)]
    public required string Name { get; set; }

    [MaxLength(36)]
    public string? ParentId { get; set; }
    
    [JsonIgnore]
    public bool HasChildren { get; set; }
    
    public string Text()
    {
        return Name;
    }

}