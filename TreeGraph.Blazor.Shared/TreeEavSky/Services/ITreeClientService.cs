using TreeGraph.Shared.TreeEavSky.Models;
using TreeGraph.Shared.TreeEavSky.Abstractions;

namespace TreeGraph.Blazor.Shared.TreeEavSky.Services;

public interface ITreeClientService<TTree> where TTree : class
{
    /// <summary>EAV 属性定义名（= TreeRoute 或 typeof(T).Name），供属性系统查询 Schema。</summary>
    string EntityType { get; }

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
