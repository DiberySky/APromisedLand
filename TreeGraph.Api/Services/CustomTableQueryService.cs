using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.Data;

namespace TreeGraph.Api.Services;

/// <summary>自定义表行内字段查询：JSONB @> 包含查询（走 ix_ctr_rowdata GIN 索引）</summary>
public class CustomTableQueryService
{
    private readonly EavDbContext _db;
    private readonly ICustomTableCache _tableCache;

    public CustomTableQueryService(EavDbContext db, ICustomTableCache tableCache)
    {
        _db = db;
        _tableCache = tableCache;
    }

    /// <summary>
    /// 查找所有含指定行数据的父实体
    /// 例：查找含 cert_name=CE 的证书的所有产品
    /// </summary>
    public async Task<List<long>> FindParentEntitiesByRowAsync(
        string parentEntityType, long attributeId,
        Dictionary<string, object?> rowFilter,
        CancellationToken ct = default)
    {
        // JsonDocument 参数保证 Npgsql 将条件翻译为 jsonb @> jsonb
        // （不 using：查询延迟执行，参数值在物化时才读取）
        var filterDoc = JsonDocument.Parse(JsonSerializer.Serialize(rowFilter));

        return await _db.CustomTableRows
            .Where(r => r.ParentEntityType == parentEntityType
                     && r.AttributeId == attributeId
                     && EF.Functions.JsonContains(r.RowData, filterDoc))
            .Select(r => r.ParentEntityId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// 组合多条件查询：每个条件匹配某一行，多条件间取交集（AND 语义）
    /// </summary>
    public async Task<List<long>> FindByMultipleRowConditionsAsync(
        string parentEntityType, long attributeId,
        List<Dictionary<string, object?>> rowConditions,
        CancellationToken ct = default)
    {
        IQueryable<long>? result = null;

        foreach (var condition in rowConditions)
        {
            var filterDoc = JsonDocument.Parse(JsonSerializer.Serialize(condition));
            var filter = filterDoc;

            var sub = _db.CustomTableRows
                .Where(r => r.ParentEntityType == parentEntityType
                         && r.AttributeId == attributeId
                         && EF.Functions.JsonContains(r.RowData, filter))
                .Select(r => r.ParentEntityId)
                .Distinct();

            result = result is null ? sub : result.Intersect(sub);
        }

        return result is null
            ? new List<long>()
            : await result.ToListAsync(ct);
    }
}
