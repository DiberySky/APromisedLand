using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Data;
using TreeGraph.Api.NodeEavSky.Entities;

namespace TreeGraph.Api.StringTreeSky.Services;

public class EntityTypeTemplateService : IEntityTypeTemplateService
{
    private readonly TreeGraphDbContext _db;

    public EntityTypeTemplateService(TreeGraphDbContext db) => _db = db;

    public async Task<TemplateCopyResult> CopyAttributesAsync(
        string sourceEntityType,
        string targetEntityType,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sourceEntityType)
            || string.IsNullOrWhiteSpace(targetEntityType))
            return new TemplateCopyResult(0, 0, new[] { "源或目标 EntityType 为空" });

        if (sourceEntityType == targetEntityType)
            return new TemplateCopyResult(0, 0, new[] { "源与目标相同，跳过" });

        var srcAttrs = await _db.AttributeCatalog
            .Where(a => a.EntityType == sourceEntityType && !a.IsDeleted)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync(ct);

        if (srcAttrs.Count == 0)
            return new TemplateCopyResult(0, 0, new[] { "源无属性可复制" });

        // 目标类型必须已注册（通常由 SpaceController 提前注册）
        var targetExists = await _db.EntityTypes
            .AnyAsync(t => t.EntityType == targetEntityType && !t.IsDeleted, ct);
        if (!targetExists)
            return new TemplateCopyResult(0, 0,
                new[] { $"目标 EntityType 未注册：{targetEntityType}" });

        int copied = 0;
        var skipped = new List<string>();

        foreach (var src in srcAttrs)
        {
            // 跳过 composite / table
            if (src.DataType is "composite" or "table")
            {
                skipped.Add($"{src.AttributeName}（{src.DataType} 暂不支持复制）");
                continue;
            }

            // 已存在同名属性则跳过（防止重复调用）
            var exists = await _db.AttributeCatalog.AnyAsync(a =>
                a.EntityType == targetEntityType
                && a.AttributeName == src.AttributeName
                && !a.IsDeleted, ct);
            if (exists)
            {
                skipped.Add($"{src.AttributeName}（目标已存在同名属性）");
                continue;
            }

            // single_choice：深拷贝选项集
            string? newOptionSetId = null;
            if (src.DataType == "single_choice" && !string.IsNullOrEmpty(src.RefOptionSetId))
            {
                newOptionSetId = await CloneOptionSetAsync(
                    src.RefOptionSetId, targetEntityType, ct);
                if (newOptionSetId is null)
                {
                    skipped.Add($"{src.AttributeName}（选项集复制失败）");
                    continue;
                }
            }

            _db.AttributeCatalog.Add(new AttributeDefinition
            {
                AttributeId = Guid.NewGuid().ToString("D"),
                EntityType = targetEntityType,
                AttributeName = src.AttributeName,
                DisplayName = src.DisplayName,
                DataType = src.DataType,
                IsRequired = src.IsRequired,
                IsSearchable = src.IsSearchable,
                IsSortable = src.IsSortable,
                IsDeleted = false,
                Version = 1,
                DisplayOrder = src.DisplayOrder,
                DefaultValue = src.DefaultValue,

                // JsonDocument 需要克隆（避免跨实体共享同一实例）
                AllowedValues = CloneJsonDoc(src.AllowedValues),
                ValidationRule = CloneJsonDoc(src.ValidationRule),

                // 单位全局共享（不复制）
                UnitId = src.UnitId,

                // 选项集指向新拷贝
                RefOptionSetId = newOptionSetId,

                // 复合/自定义表：null（v1 不支持复制）
                RefCompositeTypeId = null,
                RefTableDefinitionId = null,

                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            copied++;
        }

        await _db.SaveChangesAsync(ct);
        return new TemplateCopyResult(copied, skipped.Count, skipped);
    }

    // ============================================================
    // 内部
    // ============================================================

    private async Task<string?> CloneOptionSetAsync(
        string sourceOptionSetId,
        string targetEntityType,
        CancellationToken ct)
    {
        var src = await _db.OptionSets
            .Include(s => s.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OptionSetId == sourceOptionSetId && !s.IsDeleted, ct);
        if (src is null) return null;

        var newId = Guid.NewGuid().ToString("D");

        // SetName 可能因 partial unique 冲突，加后缀保证唯一
        var newSetName = src.SetName;
        var setNameExists = await _db.OptionSets.AnyAsync(s =>
            s.EntityType == targetEntityType
            && s.SetName == newSetName
            && !s.IsDeleted, ct);
        if (setNameExists)
            newSetName = $"{src.SetName}_{newId[..8]}";

        var newSet = new OptionSet
        {
            OptionSetId = newId,
            EntityType = targetEntityType,
            SetName = newSetName,
            DisplayName = src.DisplayName,
            IsDeleted = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        foreach (var item in src.Items.Where(i => !i.IsDeleted))
        {
            newSet.Items.Add(new OptionItem
            {
                OptionItemId = Guid.NewGuid().ToString("D"),
                OptionSetId = newId,
                Value = item.Value,
                Label = item.Label,
                DisplayOrder = item.DisplayOrder,
                IsDefault = item.IsDefault,
                IsDeleted = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        _db.OptionSets.Add(newSet);
        return newId;
    }

    /// <summary>
    /// 克隆 JsonDocument（避免 EF 跨实体共享同一实例引发的序列化问题）。
    /// </summary>
    private static JsonDocument? CloneJsonDoc(JsonDocument? src)
    {
        if (src is null) return null;
        try
        {
            return JsonDocument.Parse(src.RootElement.GetRawText());
        }
        catch
        {
            return null;
        }
    }
}
