namespace APromisedLand.Shared.DiberyTree.Interfaces;

public interface ITreeNode
{
    string Id { get; set; }
    
    string? ParentId { get; set; }
    
    public bool HasChildren { get; set; }
    
    string Text();
}