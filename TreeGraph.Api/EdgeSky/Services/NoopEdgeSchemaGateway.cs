using TreeGraph.Api.EdgeSky.Entities;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>Testing 环境空桩：无 NebulaGraph，DDL 同步发布即丢弃。</summary>
public sealed class NoopEdgeSchemaGateway : IEdgeSchemaGateway
{
    public Task CreateEdgeTypeAsync(EdgeType type, CancellationToken ct) => Task.CompletedTask;
    public Task AlterAddPropsAsync(string spaceId, string edgeType, IReadOnlyList<EdgeTypeProp> props, CancellationToken ct) => Task.CompletedTask;
    public Task AlterDropPropsAsync(string spaceId, string edgeType, IReadOnlyList<string> propNames, CancellationToken ct) => Task.CompletedTask;
    public Task DropEdgeTypeAsync(string spaceId, string edgeType, CancellationToken ct) => Task.CompletedTask;
}
