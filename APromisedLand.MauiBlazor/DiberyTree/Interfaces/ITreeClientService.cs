using APromisedLand.Shared.DiberyTree.Models;

namespace APromisedLand.MauiBlazor.DiberyTree.Interfaces;

public interface ITreeClientService<TTree> where TTree : class
{
    bool NewPageOpen { get; set; }
    
    Task<IReadOnlyList<TreeNodeDto<TTree>>> LoadInitialDataAsync(string? rootId);
    Task<IReadOnlyList<TreeNodeDto<TTree>>> LoadChildrenAsync(TTree? parent = null);
    Task<List<string>?> GetAncestorPathFromApiAsync(string nodeId);
}