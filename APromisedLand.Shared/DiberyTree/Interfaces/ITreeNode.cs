namespace APromisedLand.Shared.DiberyTree.Interfaces;

public interface ITreeNode
{
    string Id { get; }
    string? ParentId { get; }
    string Text();  // 或 Name 属性，根据实现而定
}