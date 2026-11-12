namespace TreeGraph.Api.GraphSyncSky.SpaceNode;

/// <summary>
/// 派生 Tag 的 Nebula schema 对齐器（消费者与启动对齐共用）：
/// CREATE IF NOT EXISTS → DESCRIBE → ADD 缺失列 / DROP 多余列（受管 Tag 以账本快照为准）。
/// 只作用于调用方传入的受管 Tag 名，不碰系统 Tag 与未知 Tag。
/// </summary>
public static class SpaceNodeTagSchemaAligner
{
    public static async Task EnsureTagAsync(
        NebulaGraphClient client, string space, string tagName,
        TagProjectionSchema schema, ILogger logger, CancellationToken ct)
    {
        var shown = await client.QueryAsync(space, "SHOW TAGS", ct);
        var exists = shown.Data?
            .Any(r => r.TryGetValue("Name", out var n)
                   && string.Equals(n.GetString(), tagName, StringComparison.Ordinal)) ?? false;

        if (!exists)
        {
            await client.QueryAsync(space,
                SpaceNodeTagProjector.BuildCreateTagNgql(tagName, schema.Columns), ct);
            logger.LogInformation("派生 Tag 已创建：{Space}.{Tag}（{Count} 列）",
                space, tagName, schema.Columns.Count);
            return;
        }

        var described = await client.QueryAsync(space, $"DESCRIBE TAG `{tagName}`", ct);
        var existing = described.Data?
            .Select(r => r.TryGetValue("Field", out var f) ? f.GetString() : null)
            .Where(s => !string.IsNullOrEmpty(s))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var missing = schema.Columns.Where(c => !existing.Contains(c.Name)).ToList();
        if (missing.Count > 0)
            await client.QueryAsync(space,
                SpaceNodeTagProjector.BuildAlterAddNgql(tagName, missing), ct);

        var snapshotNames = schema.Columns.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extra = existing.Where(name => !snapshotNames.Contains(name)).ToList();
        if (extra.Count > 0)
            await client.QueryAsync(space,
                SpaceNodeTagProjector.BuildAlterDropNgql(tagName, extra), ct);

        if (missing.Count > 0 || extra.Count > 0)
            logger.LogInformation(
                "派生 Tag 已对齐：{Space}.{Tag} +[{Add}] -[{Drop}]",
                space, tagName,
                string.Join(",", missing.Select(c => c.Name)), string.Join(",", extra));
    }
}
