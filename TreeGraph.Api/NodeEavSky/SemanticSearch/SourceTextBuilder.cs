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
    /// 为指定 iNode 构造宿主文本。
    /// iNode 下无任何实体时返回 null（调用方删除向量）。
    /// </summary>
    public async Task<SourceText?> BuildAsync(string inodeId, CancellationToken ct = default)
    {
        // 1. 查 inode 归属的全部实体
        var entityTypes = await _db.InodeEntities
            .AsNoTracking()
            .Where(m => m.InodeId == inodeId)
            .Select(m => new { m.EntityType, m.EntityId })
            .ToListAsync(ct);

        if (entityTypes.Count == 0)
            return null;

        // 2. 解析宿主类型与 spaceId
        var space = await _db.SpaceTrees
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == inodeId, ct);

        var hostType = space is not null ? "space_tree" : "tree_node";
        var spaceId = "default";

        // 3. 逐类型构造文本段（按实体类型名排序，保证确定性）
        var lines = new List<string>();

        if (space is not null)
            lines.Add($"节点：{space.Name}");

        foreach (var et in entityTypes
                     .OrderBy(e => e.EntityType, StringComparer.Ordinal))
        {
            lines.Add($"类型：{et.EntityType}");

            var attrs = _db.AttributeCatalog
                .Where(a => a.EntityType == et.EntityType && !a.IsDeleted)
                .ToList();
            var defs = attrs
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.AttributeId, StringComparer.Ordinal)
                .ToList();

            var values = await _db.AttributeValues
                .AsNoTracking()
                .Where(v => v.EntityType == et.EntityType && v.EntityId == et.EntityId)
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

            // 自定义表子数据（按 RowOrder 排序，限制行数防爆 token）
            var tableRows = await _db.CustomTableRows
                .AsNoTracking()
                .Where(r => r.ParentEntityType == et.EntityType &&
                            r.ParentEntityId == et.EntityId)
                .OrderBy(r => r.RowOrder)
                .Take(50)
                .ToListAsync(ct);

            foreach (var row in tableRows)
            {
                var rowText = FormatTableRow(row.RowData);
                if (!string.IsNullOrWhiteSpace(rowText))
                    lines.Add($"行：{rowText}");
            }
        }

        var text = string.Join("；", lines);
        var hash = ComputeHash(text);

        return new SourceText(text, hostType, spaceId, hash);
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

public sealed record SourceText(
    string Text, string HostType, string SpaceId, string SourceHash);
