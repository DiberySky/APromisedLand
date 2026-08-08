namespace APromisedLand.Shared.DiberyTree.Interfaces;

public interface ITreeNodeBase<TItem>
{
    string Id { get; set; }
    string? ParentId { get; set; }
    /// <summary>节点描述</summary>
    public string? Description { get; set; }
    /// <summary>是否启用</summary>
    bool CanHaveChildren { get; set; }
    int SortOrder { get; set; }
    
    bool HasChildren { get; set; }
    TItem? Parent { get; set; }
    
    string Text();  // 或 Name 属性，根据实现而定
}