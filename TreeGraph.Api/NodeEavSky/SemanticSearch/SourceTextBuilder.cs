using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;

namespace TreeGraph.Api.NodeEavSky.SemanticSearch;

/// <summary>
/// 确定性宿主文本构造器。
///
/// 输入：inodeId + 当前 PostgreSQL 已提交状态
/// 输出：(text, hostType, spaceId, sourceHash)
///
/// 确定性要求：
///   - 同一份数据库状态，任意时刻、任意进程构造出的文本与 sourceHash 完全一致
///   - 属性按 AttributeDefinition.DisplayOrder 排序，与人工认知顺序一致
///   - 不依赖运行时随机因素（不含时间戳 / 随机 ID）
///
/// 幂等的基础：消费者收到重复 / 乱序消息时，重新构造的文本与
/// sourceHash 相同，upsert 不产生实际变化（Qdrant upsert 覆盖同 PointId）。
/// </summary>
public sealed class SourceTextBuilder
{
    private readonly TreeGraphDbContext _db;

    public SourceTextBuilder(TreeGraphDbContext db) => _db = db;

    /// <summary>
    /// 为指定 iNode 构造宿主文本（节点 + 实体分离）。
    /// 返回：NodeText（纯节点名）+ EntityTexts（各实体独立文本）。
    /// </summary>
    public async Task<NodeSourceText?> BuildAsync(string inodeId, CancellationToken ct = default)
    {
        // 1. 查 inode 归属的全部实体
        var entityMounts = await _db.InodeEntities
            .AsNoTracking()
            .Where(m => m.InodeId == inodeId)
            .Select(m => new { m.EntityType, m.EntityId })
            .ToListAsync(ct);

        // 2. 解析宿主节点
        var space = await _db.SpaceTrees
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == inodeId, ct);

        if (space is null && entityMounts.Count == 0)
            return null; // 既无实体又不在树中 → 删除向量

        var hostType = space is not null ? "space_tree" : "tree_node";
        var spaceId = "default";

        // 3. 构造节点文本（纯节点名）。
        // space 节点（Id 存在于 spaces 空间实体列表内，代表空间本身）不创建向量。
        var isSpaceNode = space is not null &&
            await _db.Spaces.AsNoTracking().AnyAsync(s => s.Id == inodeId, ct);
        var nodeText = space is not null && !isSpaceNode
            ? $"节点：{space.Name}" : "";
        var nodeHash = ComputeHash(nodeText);

        // 4. 构造各实体独立文本
        var entityTexts = new List<EntitySourceText>();
        foreach (var mount in entityMounts)
        {
            var entityText = await BuildEntityTextAsync(
                mount.EntityType, mount.EntityId, ct);
            if (entityText is not null)
                entityTexts.Add(entityText);
        }

        return new NodeSourceText(
            inodeId, hostType, spaceId,
            nodeText, nodeHash, entityTexts);
    }

    /// <summary>
    /// 构造单个实体的独立文本。
    /// </summary>
    private async Task<EntitySourceText?> BuildEntityTextAsync(
        string entityType, string entityId, CancellationToken ct)
    {
        var lines = new List<string> { $"类型：{entityType}" };

        var attrs = _db.AttributeCatalog
            .Where(a => a.EntityType == entityType && !a.IsDeleted)
            .ToList();
        var defs = attrs
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.AttributeId, StringComparer.Ordinal)
            .ToList();

        var values = await _db.AttributeValues
            .AsNoTracking()
            .Where(v => v.EntityType == entityType && v.EntityId == entityId)
            .ToListAsync(ct);

        var byAttrId = values.ToDictionary(v => v.AttributeId, v => v);

        foreach (var def in defs)
        {
            if (!byAttrId.TryGetValue(def.AttributeId, out var val))
                continue;

            var display = FormatValue(val);
            if (display is null) continue;
            lines.Add($"{def.DisplayName}：{display}");
        }

        // 自定义表子数据
        var tableRows = await _db.CustomTableRows
            .AsNoTracking()
            .Where(r => r.ParentEntityType == entityType &&
                        r.ParentEntityId == entityId)
            .OrderBy(r => r.RowOrder)
            .Take(50)
            .ToListAsync(ct);

        foreach (var row in tableRows)
        {
            var rowText = FormatTableRow(row.RowData);
            if (!string.IsNullOrWhiteSpace(rowText))
                lines.Add($"行：{rowText}");
        }

        if (lines.Count <= 1) return null; // 只有类型行，无实际内容

        var text = string.Join("；", lines);
        var hash = ComputeHash(text);

        return new EntitySourceText(entityId, entityType, text, hash);
    }

    private static string? FormatValue(NodeEavSky.Entities.AttributeValue v)
    {
        if (!string.IsNullOrWhiteSpace(v.ValueString)) return v.ValueString;
        if (v.ValueInt.HasValue) return v.ValueInt.Value.ToString();
        if (v.ValueDecimal.HasValue) return v.ValueDecimal.Value.ToString("G29");
        if (v.ValueBool.HasValue) return v.ValueBool.Value ? "是" : "否";
        if (v.ValueDatetime.HasValue) return v.ValueDatetime.Value.ToString("yyyy-MM-dd HH:mm");
        if (v.ValueJsonb is not null) return v.ValueJsonb.RootElement.GetRawText();
        return null;
    }

    private static string FormatTableRow(System.Text.Json.JsonDocument rowData)
    {
        var parts = new List<string>();
        foreach (var prop in rowData.RootElement.EnumerateObject())
        {
            var val = prop.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => prop.Value.GetString(),
                System.Text.Json.JsonValueKind.Number => prop.Value.GetRawText(),
                System.Text.Json.JsonValueKind.True => "是",
                System.Text.Json.JsonValueKind.False => "否",
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(val))
                parts.Add($"{prop.Name}={val}");
        }
        return string.Join("，", parts);
    }

    /// <summary>SHA256 内容指纹（十六进制小写）。</summary>
    public static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

/// <summary>节点级源文本：纯节点文本 + 挂载的全部实体独立文本。</summary>
public sealed record NodeSourceText(
    string InodeId,
    string HostType,
    string SpaceId,
    string NodeText,
    string NodeHash,
    IReadOnlyList<EntitySourceText> EntityTexts);

/// <summary>实体级源文本：独立于节点存在，仅通过 InodeId 建立路径关系。</summary>
public sealed record EntitySourceText(
    string EntityId,
    string EntityType,
    string Text,
    string Hash);
