using System.Text.Json;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Services;

/// <summary>
/// TreeGraphEavApi 客户端。BaseAddress 由 DI 注册时通过 Aspire 服务发现注入
/// （https+http://treegrapheavapi），本类只使用相对路径。
/// </summary>
public class EavApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<EavApiClient> _logger;

    public EavApiClient(HttpClient http, ILogger<EavApiClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    // ============================================================
    // EavController
    // ============================================================

    public async Task<IReadOnlyList<AttributeSchemaDto>?> GetSchemaAsync(
        string entityType, CancellationToken ct = default)
    {
        var resp = await GetAsync<SchemaResponse>($"api/eav/{entityType}/schema", ct);
        return resp?.Attributes;
    }

    public async Task<DynamicEntityDto?> GetEntityAsync(
        string entityType, long entityId,
        bool originalUnits = false, CancellationToken ct = default)
    {
        var url = $"api/eav/{entityType}/entities/{entityId}";
        if (originalUnits) url += "?unit=original";
        return await GetAsync<DynamicEntityDto>(url, ct);
    }

    public async Task<(bool Ok, string? Error)> SaveEntityAsync(
        string entityType, long entityId,
        Dictionary<string, object?> values, CancellationToken ct = default)
        => await PutAsync($"api/eav/{entityType}/entities/{entityId}", values, ct);

    public async Task<PagedResult<DynamicEntityDto>?> QueryAsync(
        string entityType, EavQueryRequest request, CancellationToken ct = default)
        => await PostAsync<PagedResult<DynamicEntityDto>>(
            $"api/eav/{entityType}/entities/query", request, ct);

    public async Task<IReadOnlyList<long>?> QueryByTableAsync(
        string entityType, QueryByTableRequest request, CancellationToken ct = default)
    {
        var resp = await PostAsync<QueryByTableResponse>(
            $"api/eav/{entityType}/entities/query-by-table", request, ct);
        return resp?.EntityIds;
    }

    public async Task<IReadOnlyList<EntityHistoryDto>?> GetHistoryAsync(
        string entityType, long entityId,
        DateTimeOffset? from = null, CancellationToken ct = default)
    {
        var url = $"api/eav/{entityType}/entities/{entityId}/history";
        if (from.HasValue)
            url += $"?from={Uri.EscapeDataString(from.Value.ToString("O"))}";
        return await GetAsync<IReadOnlyList<EntityHistoryDto>>(url, ct);
    }

    // ============================================================
    // CustomTableDataController
    // ============================================================

    public async Task<CustomTableValue?> LoadCustomTableAsync(
        string entityType, long entityId, string tableName, CancellationToken ct = default)
        => await GetAsync<CustomTableValue>(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}", ct);

    public async Task<(bool Ok, string? Error)> ReplaceCustomTableAsync(
        string entityType, long entityId, string tableName,
        CustomTableValue value, CancellationToken ct = default)
        => await PutAsync(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}", value, ct);

    public async Task<(bool Ok, string? Error)> UpsertCustomTableRowAsync(
        string entityType, long entityId, string tableName,
        CustomTableRowValue rowValue, CancellationToken ct = default)
        => await PutAsync(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}/rows", rowValue, ct);

    /// <summary>★ 修复 P0-2：改用 DeleteWithErrorAsync 以保留后端业务错误消息</summary>
    public async Task<(bool Ok, string? Error)> DeleteCustomTableRowAsync(
        string entityType, long entityId, string tableName, long rowId,
        CancellationToken ct = default)
        => await DeleteWithErrorAsync(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}/rows/{rowId}", ct);

    // ============================================================
    // EavMetadataController - 创建
    // ============================================================

    /// <summary>★ 修复 P0-1：按属性名提取 ID</summary>
    public Task<long?> CreateAttributeAsync(
        CreateAttributeRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/attributes", request, "attributeId", ct);

    public Task<long?> CreateCompositeTypeAsync(
        CreateCompositeTypeRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/composite-types", request, "compositeTypeId", ct);

    public Task<long?> AddCompositeFieldAsync(
        long compositeTypeId, CreateCompositeFieldRequest request,
        CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields",
            request, "fieldId", ct);

    public Task<long?> CreateCustomTableAsync(
        CreateCustomTableRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/custom-tables", request, "tableDefinitionId", ct);

    public Task<long?> AddCustomTableColumnAsync(
        long tableDefinitionId, CreateTableColumnRequest request,
        CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/metadata/custom-tables/{tableDefinitionId}/columns",
            request, "columnId", ct);

    /// <summary>★ 修复 P0-2</summary>
    public Task<(bool Ok, string? Error)> DeleteAttributeAsync(
        long attributeId, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/attributes/{attributeId}", ct);

    // ============================================================
    // 组合类型
    // ============================================================

    public async Task<IReadOnlyList<CompositeTypeDetailDto>?> ListCompositeTypesAsync(
        string? entityType = null, CancellationToken ct = default)
    {
        var url = "api/eav/metadata/composite-types";
        if (!string.IsNullOrEmpty(entityType))
            url += $"?entityType={Uri.EscapeDataString(entityType)}";
        return await GetAsync<IReadOnlyList<CompositeTypeDetailDto>>(url, ct);
    }

    public Task<CompositeTypeDetailDto?> GetCompositeTypeAsync(
        long id, CancellationToken ct = default)
        => GetAsync<CompositeTypeDetailDto>($"api/eav/metadata/composite-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateCompositeTypeAsync(
        long id, UpdateCompositeTypeRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/composite-types/{id}", request, ct);

    public Task<(bool Ok, string? Error)> DeleteCompositeTypeAsync(
        long id, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/composite-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateCompositeFieldAsync(
        long compositeTypeId, long fieldId,
        UpdateCompositeFieldRequest request, CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields/{fieldId}",
            request, ct);

    public Task<(bool Ok, string? Error)> DeleteCompositeFieldAsync(
        long compositeTypeId, long fieldId, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields/{fieldId}", ct);

    // ============================================================
    // 自定义表
    // ============================================================

    public async Task<IReadOnlyList<CustomTableDetailDto>?> ListCustomTablesAsync(
        string? entityType = null, CancellationToken ct = default)
    {
        var url = "api/eav/metadata/custom-tables";
        if (!string.IsNullOrEmpty(entityType))
            url += $"?entityType={Uri.EscapeDataString(entityType)}";
        return await GetAsync<IReadOnlyList<CustomTableDetailDto>>(url, ct);
    }

    public Task<CustomTableDetailDto?> GetCustomTableAsync(
        long id, CancellationToken ct = default)
        => GetAsync<CustomTableDetailDto>($"api/eav/metadata/custom-tables/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateCustomTableAsync(
        long id, UpdateCustomTableRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/custom-tables/{id}", request, ct);

    public Task<(bool Ok, string? Error)> DeleteCustomTableAsync(
        long id, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/custom-tables/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateTableColumnAsync(
        long tableId, long columnId,
        UpdateTableColumnRequest request, CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/custom-tables/{tableId}/columns/{columnId}",
            request, ct);

    public Task<(bool Ok, string? Error)> DeleteTableColumnAsync(
        long tableId, long columnId, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/eav/metadata/custom-tables/{tableId}/columns/{columnId}", ct);

    // ============================================================
    // 属性查询/更新
    // ============================================================

    public async Task<IReadOnlyList<AttributeDetailDto>?> ListAttributesAsync(
        string? entityType = null, bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(entityType))
            query.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (includeDeleted) query.Add("includeDeleted=true");

        var url = "api/eav/metadata/attributes";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        return await GetAsync<IReadOnlyList<AttributeDetailDto>>(url, ct);
    }

    public Task<AttributeDetailDto?> GetAttributeAsync(
        long id, CancellationToken ct = default)
        => GetAsync<AttributeDetailDto>($"api/eav/metadata/attributes/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateAttributeAsync(
        long id, UpdateAttributeRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/attributes/{id}", request, ct);

    // ============================================================
    // 选项集
    // ============================================================

    public Task<long?> CreateOptionSetAsync(
        CreateOptionSetRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/option-sets", request, "optionSetId", ct);

    public async Task<IReadOnlyList<OptionSetSummaryDto>?> ListOptionSetsAsync(
        string? entityType = null, CancellationToken ct = default)
    {
        var url = "api/eav/metadata/option-sets";
        if (!string.IsNullOrEmpty(entityType))
            url += $"?entityType={Uri.EscapeDataString(entityType)}";
        return await GetAsync<IReadOnlyList<OptionSetSummaryDto>>(url, ct);
    }

    /// <summary>★ 修复 P0-2</summary>
    public Task<(bool Ok, string? Error)> DeleteOptionSetAsync(
        long optionSetId, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/option-sets/{optionSetId}", ct);

    public Task<OptionSetDetailDto?> GetOptionSetAsync(
        long optionSetId, CancellationToken ct = default)
        => GetAsync<OptionSetDetailDto>(
            $"api/eav/metadata/option-sets/{optionSetId}", ct);

    public Task<(bool Ok, string? Error)> UpdateOptionSetAsync(
        long optionSetId, UpdateOptionSetRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/option-sets/{optionSetId}", request, ct);

    public Task<IReadOnlyList<OptionSetReferenceDto>?> GetOptionSetReferencesAsync(
        long optionSetId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<OptionSetReferenceDto>>(
            $"api/eav/metadata/option-sets/{optionSetId}/references", ct);

    // ============================================================
    // 选项项
    // ============================================================

    public Task<IReadOnlyList<OptionItemDetailDto>?> ListOptionItemsAsync(
        long optionSetId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<OptionItemDetailDto>>(
            $"api/eav/metadata/option-sets/{optionSetId}/items", ct);

    public Task<long?> AddOptionItemAsync(
        long optionSetId, CreateOptionItemRequest request,
        CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items",
            request, "optionItemId", ct);

    public Task<(bool Ok, string? Error)> UpdateOptionItemAsync(
        long optionSetId, long itemId, UpdateOptionItemRequest request,
        CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/{itemId}",
            request, ct);

    /// <summary>★ 修复 P0-2</summary>
    public Task<(bool Ok, string? Error)> DeleteOptionItemAsync(
        long optionSetId, long itemId, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/{itemId}", ct);

    public Task<(bool Ok, string? Error)> ReorderOptionItemsAsync(
        long optionSetId, List<ReorderOptionItem> orders,
        CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/reorder",
            orders, ct);

    // ============================================================
    // 单位
    // ============================================================

    public async Task<IReadOnlyList<UnitDto>?> ListUnitsAsync(
        string? category = null, CancellationToken ct = default)
    {
        var url = "api/units";
        if (!string.IsNullOrEmpty(category))
            url += $"?category={Uri.EscapeDataString(category)}";
        return await GetAsync<IReadOnlyList<UnitDto>>(url, ct);
    }

    public Task<IReadOnlyList<UnitCategoryDto>?> GetUnitCategoriesAsync(
        CancellationToken ct = default)
        => GetAsync<IReadOnlyList<UnitCategoryDto>>("api/units/categories", ct);

    public async Task<Guid?> CreateUnitAsync(
        CreateUnitRequest request, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/units", request, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("POST api/units → {Status}", resp.StatusCode);
                return null;
            }
            var doc = await resp.Content.ReadFromJsonAsync<IdResponse<Guid>>(JsonOptions, ct);
            return doc?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST api/units 失败");
            return null;
        }
    }

    // ============================================================
    // 通用助手
    // ============================================================

    private async Task<T?> GetAsync<T>(string relativeUrl, CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync(relativeUrl, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Url} → {Status}", relativeUrl, resp.StatusCode);
                return default;
            }
            return await resp.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} 失败", relativeUrl);
            return default;
        }
    }

    private async Task<T?> PostAsync<T>(
        string relativeUrl, object body, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(relativeUrl, body, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("POST {Url} → {Status}", relativeUrl, resp.StatusCode);
                return default;
            }
            return await resp.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", relativeUrl);
            return default;
        }
    }

    private async Task<(bool Ok, string? Error)> PutAsync(
        string relativeUrl, object body, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PutAsJsonAsync(relativeUrl, body, JsonOptions, ct);
            if (resp.IsSuccessStatusCode) return (true, null);

            // ★ 修复 P1-5：解析后端业务错误消息，而不是直接暴露原始 JSON
            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("PUT {Url} → {Status}: {Error}",
                relativeUrl, resp.StatusCode, msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT {Url} 失败", relativeUrl);
            return (false, ex.Message);
        }
    }

    private async Task<(bool Ok, string? Error)> DeleteWithErrorAsync(
        string relativeUrl, CancellationToken ct)
    {
        try
        {
            var resp = await _http.DeleteAsync(relativeUrl, ct);
            if (resp.IsSuccessStatusCode) return (true, null);

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("DELETE {Url} → {Status}: {Error}",
                relativeUrl, resp.StatusCode, msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DELETE {Url} 失败", relativeUrl);
            return (false, ex.Message);
        }
    }

    /// <summary>解析后端 400/409 响应体中的 { error } / { errors[] }</summary>
    private static async Task<string?> ExtractErrorAsync(
        HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var body = await resp.Content
                .ReadFromJsonAsync<ErrorResponse>(JsonOptions, ct);
            if (body?.Errors is { Count: > 0 })
                return string.Join("; ",
                    body.Errors.Select(e => $"{e.Field}: {e.Message}"));
            return body?.Error ?? resp.ReasonPhrase;
        }
        catch
        {
            return resp.ReasonPhrase;
        }
    }

    /// <summary>★ 修复 P0-1：按属性名取 ID，不再"取第一个数字"</summary>
    private async Task<long?> PostForIdAsync(
        string relativeUrl, object body, string idPropertyName,
        CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(relativeUrl, body, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("POST {Url} → {Status}: {Body}",
                    relativeUrl, resp.StatusCode, text);
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(
                await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (doc.RootElement.TryGetProperty(idPropertyName, out var p)
                && p.ValueKind == JsonValueKind.Number)
                return p.GetInt64();

            _logger.LogWarning(
                "POST {Url} → 响应缺少 {IdProperty}: {Body}",
                relativeUrl, idPropertyName, doc.RootElement.GetRawText());
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", relativeUrl);
            return null;
        }
    }

    private sealed record SchemaResponse(
        string EntityType, IReadOnlyList<AttributeSchemaDto> Attributes);

    private sealed record QueryByTableResponse(List<long> EntityIds);

    private sealed record IdResponse<T>(T Id);

    /// <summary>后端错误响应：{ error } 或 { errors: [{ field, message }] }</summary>
    private sealed record ErrorResponse(
        string? Error, List<ValidationErrorDto>? Errors);

    private sealed record ValidationErrorDto(string Field, string Message);
}
