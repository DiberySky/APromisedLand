namespace APromisedLand.Shared.DiberyTree.Interfaces;

public interface ITreeNodeBase
{
    string Id { get; }
    string? ParentId { get; }
    /// <summary>节点描述</summary>
    public string? Description { get; set; }
    /// <summary>是否启用</summary>
    bool IsActive { get; set; }
    int SortOrder { get; set; }
    
    string Text();  // 或 Name 属性，根据实现而定
    bool HasChildren { get; }
}