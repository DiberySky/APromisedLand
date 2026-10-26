using TreeGraph.Api.EdgeSky.Entities;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// NebulaGraph 边 Schema DDL 同步网关（CREATE/ALTER/DROP EDGE）。
/// EdgeType 从属于图空间：spaceId 经 <see cref="GraphSyncSky.GraphNaming.ToNebulaSpace"/>
/// 映射为目标 Nebula space。PG edge_types 是唯一真源；DDL 经本网关同步到图库，
/// 失败由启动时 GraphSchemaInitializer 与全量重建兜底对齐。
/// Testing 环境（无 NebulaGraph）注册 <see cref="NoopEdgeSchemaGateway"/> 空桩。
/// </summary>
public interface IEdgeSchemaGateway
{
    /// <summary>CREATE EDGE（含属性列/注释/TTL），并等待 DESCRIBE 可见。</summary>
    Task CreateEdgeTypeAsync(EdgeType type, CancellationToken ct);

    /// <summary>ALTER EDGE ADD（新增属性列），并等待列可见。</summary>
    Task AlterAddPropsAsync(string spaceId, string edgeType, IReadOnlyList<EdgeTypeProp> props, CancellationToken ct);

    /// <summary>ALTER EDGE DROP（删除属性列），并等待列消失。</summary>
    Task AlterDropPropsAsync(string spaceId, string edgeType, IReadOnlyList<string> propNames, CancellationToken ct);

    /// <summary>ALTER EDGE CHANGE（属性类型变更，仅限 Nebula 白名单转换）。</summary>
    Task AlterChangePropTypeAsync(string spaceId, string edgeType, string propName, string newDataType, CancellationToken ct);

    /// <summary>DROP EDGE（IF EXISTS），并等待 DESCRIBE 不再可见。</summary>
    Task DropEdgeTypeAsync(string spaceId, string edgeType, CancellationToken ct);
}
