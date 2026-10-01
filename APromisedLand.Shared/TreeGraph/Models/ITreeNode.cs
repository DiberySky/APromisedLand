namespace APromisedLand.Shared.TreeGraph.Models;

/// <summary>
/// 泛型树节点契约。TKey 必须为非空类型(推荐 string GUID)。
/// 前后端共享,通过 JSON 通信时字段名对齐即可。
/// </summary>
public interface ITreeNode<TKey> where TKey : notnull
{
    TKey Id { get; set; }
    TKey? ParentId { get; set; }
    string Text { get; set; }
    string? Icon { get; set; }
    int SortOrder { get; set; }
    bool HasChildren { get; set; }
}
