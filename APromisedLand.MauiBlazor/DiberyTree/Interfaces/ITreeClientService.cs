using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.MauiBlazor.DiberyTree.Interfaces;

public interface ITreeClientService<TTree> where TTree : class
{
    string Title { get; set; }
    bool NewPageShow { get; set; }
    bool SelectLeaf { get; set; }
    
    Task<IReadOnlyList<TreeNodeDto<TTree>>> LoadInitialDataAsync(string? rootId);
    Task<IReadOnlyList<TreeNodeDto<TTree>>> LoadChildrenAsync(TTree? parent = null);
    Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId);
}