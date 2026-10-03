using System.Net;
using System.Net.Http.Json;

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

    private sealed record CreateTypeResponse(
        string EntityTypeId, string EntityType);
}
