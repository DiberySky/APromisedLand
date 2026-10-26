using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;
using TreeGraph.Api.EdgeSky.Entities;
using TreeGraph.Shared.EdgeSky;
using TreeGraph.Shared.EdgeSky.Dtos;

namespace TreeGraph.Api.EdgeSky.Services;

/// <summary>
/// EdgeType 管理服务（镜像 NebulaGraph 边类型语义）：
/// 名称图空间内唯一、创建后不可改名；TTL/注释仅创建时可设；
/// 变更仅限 ALTER ADD/DROP/CHANGE 属性。PG edge_types 为唯一真源，
/// Nebula DDL 经 <see cref="IEdgeSchemaGateway"/> 同步（失败记日志，
/// 由 GraphSchemaInitializer 启动对齐 / 全量重建兜底）。
/// </summary>
public sealed class EdgeTypeService
{
    private readonly TreeGraphDbContext _db;
    private readonly IEdgeSchemaGateway _gateway;
    private readonly ILogger<EdgeTypeService> _logger;

    /// <summary>Nebula 命名规则：ASCII 字母开头，仅字母/数字/下划线（长度受 DB 列约束）。</summary>
    private static readonly Regex NameRegex = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public EdgeTypeService(
        TreeGraphDbContext db, IEdgeSchemaGateway gateway, ILogger<EdgeTypeService> logger)
    {
        _db = db;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<List<EdgeTypeDto>> ListAsync(CancellationToken ct)
    {
        var types = await _db.EdgeTypes.AsNoTracking()
            .Include(t => t.Props)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return types.Select(ToDto).ToList();
    }

    public async Task<EdgeTypeDto> GetAsync(string name, CancellationToken ct)
    {
        var type = await _db.EdgeTypes.AsNoTracking()
            .Include(t => t.Props)
            .FirstOrDefaultAsync(t => t.Name == name, ct);
        return type is null
            ? throw new KeyNotFoundException($"边类型 '{name}' 不存在")
            : ToDto(type);
    }

    /// <summary>创建 EdgeType（名称重复 → 409；校验失败 → 400）。</summary>
    public async Task<EdgeTypeDto> CreateAsync(CreateEdgeTypeRequest req, CancellationToken ct)
    {
        ValidateName(req.Name, "边类型名");

        if (await _db.EdgeTypes.AnyAsync(t => t.Name == req.Name, ct))
            throw new InvalidOperationException($"边类型 '{req.Name}' 已存在");

        var props = BuildProps(req.Name, req.Props);
        ValidateTtl(req.TtlCol, req.TtlDurationSeconds, props);

        var type = new EdgeType
        {
            Name = req.Name,
            Comment = req.Comment,
            DisplayName = req.DisplayName,
            TtlCol = req.TtlCol,
            TtlDurationSeconds = req.TtlDurationSeconds,
            Props = props
        };
        _db.EdgeTypes.Add(type);
        await _db.SaveChangesAsync(ct);

        await SyncDdlAsync(() => _gateway.CreateEdgeTypeAsync(type, ct), $"CREATE EDGE {type.Name}");
        return ToDto(type);
    }

    /// <summary>
    /// ALTER EDGE：仅属性集变更（增/删/改类型）。名称/TTL/注释不可变更（Nebula 能力对齐）。
    /// </summary>
    public async Task<EdgeTypeDto> AlterAsync(string name, AlterEdgeTypeRequest req, CancellationToken ct)
    {
        var type = await _db.EdgeTypes.Include(t => t.Props)
            .FirstOrDefaultAsync(t => t.Name == name, ct)
        ?? throw new KeyNotFoundException($"边类型 '{name}' 不存在");

        // ── 校验阶段（全部通过才落库） ──
        var addProps = BuildProps(name, req.AddProps);
        foreach (var p in addProps)
            if (type.Props.Any(x => x.PropName == p.PropName))
                throw new ArgumentException($"属性 '{p.PropName}' 已存在，无法重复新增");

        foreach (var propName in req.DropProps)
        {
            if (type.Props.All(x => x.PropName != propName))
                throw new ArgumentException($"属性 '{propName}' 不存在，无法删除");
            if (type.TtlCol == propName)
                throw new ArgumentException($"属性 '{propName}' 是 TTL 列，不可删除（Nebula 约束）");
        }

        foreach (var change in req.ChangeProps)
        {
            var existing = type.Props.FirstOrDefault(x => x.PropName == change.PropName)
                ?? throw new ArgumentException($"属性 '{change.PropName}' 不存在，无法变更");
            if (!string.IsNullOrEmpty(change.NewDataType))
            {
                if (!EdgePropDataTypes.IsValid(change.NewDataType))
                    throw new ArgumentException($"属性 '{change.PropName}' 目标类型 '{change.NewDataType}' 无效");
                if (!IsAllowedTypeChange(existing.DataType, change.NewDataType))
                    throw new ArgumentException(
                        $"属性 '{change.PropName}' 不支持 {existing.DataType} → {change.NewDataType} 转换" +
                        "（Nebula ALTER CHANGE 白名单仅含 fixed_string→string、float→double、int 变宽、定长字符串加长）");
            }

            // 可空/默认值（PG 元数据，Nebula 无对应 ALTER 语义）：改 NOT NULL 须保证生效默认值非空
            var effectiveNullable = change.Nullable ?? existing.Nullable;
            var effectiveDefault = change.DefaultValueSet ? change.DefaultValue : existing.DefaultValue;
            if (!effectiveNullable && effectiveDefault is null)
                throw new ArgumentException(
                    $"属性 '{change.PropName}' 为 NOT NULL，必须配置默认值（Nebula 约束）");
        }

        // ── 落库阶段 ──
        foreach (var p in addProps)
            type.Props.Add(p);
        foreach (var propName in req.DropProps)
            type.Props.Remove(type.Props.First(x => x.PropName == propName));
        foreach (var change in req.ChangeProps)
        {
            var prop = type.Props.First(x => x.PropName == change.PropName);
            if (!string.IsNullOrEmpty(change.NewDataType))
                prop.DataType = change.NewDataType;
            if (change.Nullable is { } nullable)
                prop.Nullable = nullable;
            if (change.DefaultValueSet)
                prop.DefaultValue = change.DefaultValue;
        }
        type.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        // ── DDL 同步阶段（失败不翻请求，由启动对齐/重建兜底） ──
        await SyncDdlAsync(() => _gateway.AlterAddPropsAsync(name, addProps, ct), $"ALTER EDGE {name} ADD");
        await SyncDdlAsync(() => _gateway.AlterDropPropsAsync(name, req.DropProps, ct), $"ALTER EDGE {name} DROP");
        foreach (var change in req.ChangeProps)
        {
            // 仅类型变更同步 Nebula DDL；可空/默认值变更仅落 PG 元数据（写入校验依据）
            if (string.IsNullOrEmpty(change.NewDataType))
                continue;
            await SyncDdlAsync(
                () => _gateway.AlterChangePropTypeAsync(name, change.PropName, change.NewDataType, ct),
                $"ALTER EDGE {name} CHANGE {change.PropName}");
        }

        return ToDto(type);
    }

    /// <summary>更新边类型中文显示名（非 Nebula 概念，不走 DDL 同步）。</summary>
    public async Task<EdgeTypeDto> UpdateDisplayNameAsync(
        string name, string? displayName, CancellationToken ct)
    {
        var type = await _db.EdgeTypes.Include(t => t.Props)
            .FirstOrDefaultAsync(t => t.Name == name, ct)
        ?? throw new KeyNotFoundException($"边类型 '{name}' 不存在");

        type.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        type.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(type);
    }

    /// <summary>更新边属性中文显示名（非 Nebula 概念，仅 UI 展示；空白 = 清除）。</summary>
    public async Task<EdgeTypeDto> UpdatePropDisplayNameAsync(
        string name, string propName, string? displayName, CancellationToken ct)
    {
        var type = await _db.EdgeTypes.Include(t => t.Props)
            .FirstOrDefaultAsync(t => t.Name == name, ct)
        ?? throw new KeyNotFoundException($"边类型 '{name}' 不存在");

        var prop = type.Props.FirstOrDefault(p => p.PropName == propName)
        ?? throw new KeyNotFoundException($"边类型 '{name}' 的属性 '{propName}' 不存在");

        prop.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        type.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(type);
    }

    /// <summary>删除 EdgeType（仍有边实例 → 409）。</summary>
    public async Task DeleteAsync(string name, CancellationToken ct)
    {
        var type = await _db.EdgeTypes.Include(t => t.Props)
            .FirstOrDefaultAsync(t => t.Name == name, ct)
        ?? throw new KeyNotFoundException($"边类型 '{name}' 不存在");

        if (await _db.Edges.AnyAsync(e => e.EdgeType == name, ct))
            throw new InvalidOperationException($"边类型 '{name}' 仍存在边实例，无法删除（请先删除全部实例）");

        _db.EdgeTypeProps.RemoveRange(type.Props);
        _db.EdgeTypes.Remove(type);
        await _db.SaveChangesAsync(ct);

        await SyncDdlAsync(() => _gateway.DropEdgeTypeAsync(name, ct), $"DROP EDGE {name}");
    }

    // ------------------------------------------------------------
    // 校验
    // ------------------------------------------------------------

    private static void ValidateName(string name, string what)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || !NameRegex.IsMatch(name))
            throw new ArgumentException($"{what} '{name}' 不符合 Nebula 命名规则（字母开头，仅字母/数字/下划线，≤100 字符）");
    }

    private static List<EdgeTypeProp> BuildProps(string edgeTypeName, List<EdgeTypePropDto> inputs)
    {
        var props = new List<EdgeTypeProp>(inputs.Count);
        foreach (var input in inputs)
        {
            ValidateName(input.PropName, "属性名");
            if (input.PropName.Length > 200)
                throw new ArgumentException($"属性名 '{input.PropName}' 超长（≤200 字符）");
            if (!EdgePropDataTypes.IsValid(input.DataType))
                throw new ArgumentException(
                    $"属性 '{input.PropName}' 数据类型 '{input.DataType}' 无效（支持 {string.Join("/", EdgePropDataTypes.All)}）");

            JsonElement? defaultValue = input.DefaultValue;
            if (defaultValue is { ValueKind: JsonValueKind.Null })
                throw new ArgumentException($"属性 '{input.PropName}' 默认值不能为 null（不需要默认值请省略该字段）");
            if (defaultValue is { } dv)
            {
                var err = EdgePropValueValidator.Validate(input.DataType, dv);
                if (err is not null)
                    throw new ArgumentException($"属性 '{input.PropName}' 默认值无效：{err}");
            }
            if (!input.Nullable && defaultValue is null)
                throw new ArgumentException($"属性 '{input.PropName}' 为 NOT NULL，必须配置默认值（Nebula 约束）");

            if (props.Any(p => p.PropName == input.PropName))
                throw new ArgumentException($"属性 '{input.PropName}' 重复定义");

            props.Add(new EdgeTypeProp
            {
                EdgeTypeName = edgeTypeName,
                PropName = input.PropName,
                DataType = input.DataType,
                Nullable = input.Nullable,
                DefaultValue = defaultValue,
                Comment = input.Comment,
                DisplayName = input.DisplayName,
                DisplayOrder = input.DisplayOrder
            });
        }
        return props;
    }

    private static void ValidateTtl(string? ttlCol, long? ttlDuration, List<EdgeTypeProp> props)
    {
        if ((ttlCol is null) != (ttlDuration is null))
            throw new ArgumentException("TTL 列与 TTL 时长必须成对提供（Nebula 约束：TTL_COL 须配 TTL_DURATION）");
        if (ttlCol is null) return;

        if (ttlDuration!.Value <= 0)
            throw new ArgumentException("TTL 时长必须为正整数（秒）");

        var col = props.FirstOrDefault(p => p.PropName == ttlCol)
            ?? throw new ArgumentException($"TTL 列 '{ttlCol}' 必须是本类型已定义的属性");
        if (!EdgePropDataTypes.IsTtlCompatible(col.DataType))
            throw new ArgumentException($"TTL 列 '{ttlCol}' 类型须为 INT/TIMESTAMP（当前 {col.DataType}）");
    }

    /// <summary>
    /// Nebula ALTER CHANGE 类型转换白名单：fixed_string→string、float→double、int 变宽、
    /// 定长字符串加长。当前类型子集（STRING/INT/DOUBLE/BOOL/TIMESTAMP/DATE）不在白名单覆盖面内，
    /// 暂无合法转换。
    /// </summary>
    private static bool IsAllowedTypeChange(string from, string to) => false;

    // ------------------------------------------------------------
    // DDL 同步（失败不翻请求；PG 为唯一真源，图库由启动对齐/重建兜底）
    // ------------------------------------------------------------

    private async Task SyncDdlAsync(Func<Task> ddl, string what)
    {
        try
        {
            await ddl();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NebulaGraph DDL 同步失败（{What}），PG 已提交，图库 schema 由启动对齐/全量重建兜底", what);
        }
    }

    private static EdgeTypeDto ToDto(EdgeType t) => new()
    {
        Name = t.Name,
        DisplayName = t.DisplayName,
        Comment = t.Comment,
        TtlCol = t.TtlCol,
        TtlDurationSeconds = t.TtlDurationSeconds,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
        Props = t.Props.OrderBy(p => p.DisplayOrder).ThenBy(p => p.PropName).Select(p => new EdgeTypePropDto
        {
            PropName = p.PropName,
            DataType = p.DataType,
            Nullable = p.Nullable,
            DefaultValue = p.DefaultValue,
            Comment = p.Comment,
            DisplayName = p.DisplayName,
            DisplayOrder = p.DisplayOrder
        }).ToList()
    };
}
