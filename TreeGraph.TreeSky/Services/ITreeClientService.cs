using TreeGraph.TreeSky.Models;

namespace TreeGraph.TreeSky.Services;

public interface ITreeClientService<TTree> where TTree : class
{
    string Title { get; set; }
    bool NewPageShow { get; set; }
    bool SelectLeaf { get; set; }

    Task<IReadOnlyList<TreeNodeDto<TTree>>> LoadInitialDataAsync(
        string? rootId, CancellationToken ct = default);
    Task<IReadOnlyList<TreeNodeDto<TTree>>> LoadChildrenAsync(
        TTree? parent = null, CancellationToken ct = default);
    Task<List<string>?> GetAncestorPathFromApiAsync(
        string nodeId, CancellationToken ct = default);
}
