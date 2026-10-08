using System.Net;
using System.Net.Http.Json;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Blazor.E2E.Tests.Fixtures;

/// <summary>
/// 元数据页面 E2E 的公共辅助。
/// </summary>
public static class MetadataHelpers
{
    /// <summary>生成唯一名称（用于 DisplayName / Name）。</summary>
    public static string Unique(string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return $"{prefix}_{suffix}";
    }

    /// <summary>
    /// 通过 API 创建实体类型。返回 (EntityTypeId, EntityType, DisplayName)。
    /// DisplayName 必须唯一 —— 用 Unique() 生成。
    /// </summary>
    public static async Task<(string Id, string Name, string DisplayName)>
        CreateEntityTypeAsync(HttpClient http, string displayName)
    {
        var resp = await http.PostAsJsonAsync("/api/eav/entity-types",
            new { displayName });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content
            .ReadFromJsonAsync<CreateTypeResponse>();
        return (result!.EntityTypeId, result.EntityType, displayName);
    }

    /// <summary>删除实体类型（尽力而为，失败忽略）。</summary>
    public static async Task TryDeleteEntityTypeAsync(
        HttpClient http, string entityTypeId)
    {
        try
        {
            await http.DeleteAsync($"/api/eav/entity-types/{entityTypeId}");
        }
        catch { /* 忽略 */ }
    }

    /// <summary>
    /// 按 DisplayName 反查并删除实体类型（兜底清理）。
    /// 用于 UI 创建的测试（拿不到 ID），失败忽略不阻断测试。
    /// </summary>
    public static async Task TryDeleteEntityTypeByNameAsync(
        HttpClient http, string displayName)
    {
        try
        {
            var list = await http.GetFromJsonAsync<List<EntityTypeDetailDto>>(
                "/api/eav/entity-types/details");
            var target = list?.FirstOrDefault(t => t.DisplayName == displayName);
            if (target is not null)
                await http.DeleteAsync($"/api/eav/entity-types/{target.EntityTypeId}");
        }
        catch { /* 忽略 */ }
    }

    /// <summary>通过 API 创建属性。</summary>
    public static async Task CreateAttributeAsync(
        HttpClient http, string entityType, string attributeName,
        string displayName, string dataType = "string")
    {
        var resp = await http.PostAsJsonAsync("/api/eav/metadata/attributes",
            new
            {
                entityType,
                attributeName,
                displayName,
                dataType,
                isSearchable = true,
                displayOrder = 1
            });
        resp.EnsureSuccessStatusCode();
    }

    // ============================================================
    // OptionSet
    // ============================================================

    /// <summary>
    /// 按 SetName 反查并删除选项集（兜底清理）。
    ///
    /// 行为：
    ///   - 若选项集被属性引用，DeleteSet 会拒绝（400），此时静默跳过
    ///   - 若不存在，静默跳过
    ///   - 任何异常都不抛出，避免影响测试主体
    ///
    /// 用于 E2E 每个场景的 finally 块，防止 DB 累积膨胀。
    /// </summary>
    public static async Task TryDeleteOptionSetByNameAsync(
        HttpClient http, string setName)
    {
        try
        {
            var list = await http.GetFromJsonAsync<List<OptionSetSummaryDto>>(
                "/api/eav/metadata/option-sets");
            var target = list?.FirstOrDefault(s => s.SetName == setName);
            if (target is null) return;

            // DeleteSet 会级联软删 items（后端实现）
            await http.DeleteAsync(
                $"/api/eav/metadata/option-sets/{target.OptionSetId}");
        }
        catch { /* 忽略 */ }
    }

    /// <summary>
    /// 按 OptionSetId 删除选项集（用于已知 ID 的场景）。
    /// </summary>
    public static async Task TryDeleteOptionSetAsync(
        HttpClient http, string optionSetId)
    {
        try
        {
            await http.DeleteAsync(
                $"/api/eav/metadata/option-sets/{optionSetId}");
        }
        catch { /* 忽略 */ }
    }

    private sealed record CreateTypeResponse(
        string EntityTypeId, string EntityType);
}
