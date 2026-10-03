using System.Text.Json;
using TreeGraph.Shared.Eav.Dtos;

namespace TreeGraph.Blazor.Services;

/// <summary>
/// TreeGraphEavApi 客户端。
///
/// ★ 所有实体 ID / 元数据 ID 均为 GUID 字符串（36 字符）。
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
        string entityType, string entityId,
        bool originalUnits = false, CancellationToken ct = default)
    {
        var url = $"api/eav/{entityType}/entities/{entityId}";
        if (originalUnits) url += "?unit=original";
        return await GetAsync<DynamicEntityDto>(url, ct);
    }

    public async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        SaveEntityAsync(
        string entityType, string entityId,
        Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"api/eav/{entityType}/entities/{entityId}";
            using var req = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(values, options: JsonOptions)
            };

            if (expectedUpdatedAt is { } expected)
            {
                req.Headers.Add("X-Expected-Updated-At",
                    expected.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            }

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null, null);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PUT {Url} → 409 冲突：{Body}", url, text);

                DateTimeOffset? currentUpdatedAt = null;
                try
                {
                    var conflict = JsonSerializer.Deserialize<ConflictResponse>(text, JsonOptions);
                    currentUpdatedAt = conflict?.CurrentUpdatedAt;
                }
                catch (Exception parseEx)
                {
                    _logger.LogWarning(parseEx, "解析 409 响应体失败");
                }

                return (false,
                    "并发冲突：实体已被其他用户修改，请刷新后重试",
                    currentUpdatedAt);
            }

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("PUT {Url} → {Status}: {Error}", url, resp.StatusCode, msg);
            return (false, msg, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT entities 失败");
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        PatchEntityAsync(
        string entityType, string entityId,
        Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"api/eav/{entityType}/entities/{entityId}";
            using var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = JsonContent.Create(values, options: JsonOptions)
            };

            if (expectedUpdatedAt is { } expected)
            {
                req.Headers.Add("X-Expected-Updated-At",
                    expected.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            }

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null, null);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PATCH {Url} → 409：{Body}", url, text);

                DateTimeOffset? currentUpdatedAt = null;
                try
                {
                    var conflict = JsonSerializer.Deserialize<ConflictResponse>(text, JsonOptions);
                    currentUpdatedAt = conflict?.CurrentUpdatedAt;
                }
                catch { }

                return (false,
                    "并发冲突：实体已被其他用户修改，请刷新后重试",
                    currentUpdatedAt);
            }

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("PATCH {Url} → {Status}: {Error}", url, resp.StatusCode, msg);
            return (false, msg, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PATCH entities 失败");
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteEntityAsync(
        string entityType, string entityId, CancellationToken ct = default)
        => await DeleteWithErrorAsync(
            $"api/eav/{entityType}/entities/{entityId}", ct);

    public async Task<BatchDeleteResultDto?> BatchDeleteEntitiesAsync(
        string entityType,
        IReadOnlyList<string> entityIds,
        CancellationToken ct = default)
    {
        var body = new BatchDeleteRequest { EntityIds = entityIds.ToList() };
        return await PostAsync<BatchDeleteResultDto>(
            $"api/eav/{entityType}/entities/batch-delete", body, ct);
    }

    public async Task<PagedResult<DynamicEntityDto>?> QueryAsync(
        string entityType, EavQueryRequest request, CancellationToken ct = default)
        => await PostAsync<PagedResult<DynamicEntityDto>>(
            $"api/eav/{entityType}/entities/query", request, ct);

    public async Task<IReadOnlyList<string>?> QueryByTableAsync(
        string entityType, QueryByTableRequest request, CancellationToken ct = default)
    {
        var resp = await PostAsync<QueryByTableResponse>(
            $"api/eav/{entityType}/entities/query-by-table", request, ct);
        return resp?.EntityIds;
    }

    public async Task<IReadOnlyList<EntityHistoryDto>?> GetHistoryAsync(
        string entityType, string entityId,
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
        string entityType, string entityId, string tableName, CancellationToken ct = default)
        => await GetAsync<CustomTableValue>(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}", ct);

    public async Task<(bool Ok, string? Error)> ReplaceCustomTableAsync(
        string entityType, string entityId, string tableName,
        CustomTableValue value, CancellationToken ct = default)
        => await PutAsync(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}", value, ct);

    public async Task<(bool Ok, string? Error)> UpsertCustomTableRowAsync(
        string entityType, string entityId, string tableName,
        CustomTableRowValue rowValue, CancellationToken ct = default)
        => await PutAsync(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}/rows", rowValue, ct);

    public async Task<(bool Ok, string? Error)> DeleteCustomTableRowAsync(
        string entityType, string entityId, string tableName, string rowId,
        CancellationToken ct = default)
        => await DeleteWithErrorAsync(
            $"api/eav/{entityType}/entities/{entityId}/tables/{tableName}/rows/{rowId}", ct);

    // ============================================================
    // EavMetadataController - 创建
    // ============================================================

    public Task<string?> CreateAttributeAsync(
        CreateAttributeRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/attributes", request, "attributeId", ct);

    public Task<string?> CreateCompositeTypeAsync(
        CreateCompositeTypeRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/composite-types", request, "compositeTypeId", ct);

    public Task<string?> AddCompositeFieldAsync(
        string compositeTypeId, CreateCompositeFieldRequest request,
        CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields",
            request, "fieldId", ct);

    public Task<string?> CreateCustomTableAsync(
        CreateCustomTableRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/custom-tables", request, "tableDefinitionId", ct);

    public Task<string?> AddCustomTableColumnAsync(
        string tableDefinitionId, CreateTableColumnRequest request,
        CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/metadata/custom-tables/{tableDefinitionId}/columns",
            request, "columnId", ct);

    public Task<(bool Ok, string? Error)> DeleteAttributeAsync(
        string attributeId, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/attributes/{attributeId}", ct);

    // ============================================================
    // 组合类型
    // ============================================================

    public async Task<IReadOnlyList<CompositeTypeDetailDto>?> ListCompositeTypesAsync(
        string? entityType = null,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(entityType))
            query.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (includeDeleted) query.Add("includeDeleted=true");

        var url = "api/eav/metadata/composite-types";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        return await GetAsync<IReadOnlyList<CompositeTypeDetailDto>>(url, ct);
    }

    public Task<CompositeTypeDetailDto?> GetCompositeTypeAsync(
        string id, CancellationToken ct = default)
        => GetAsync<CompositeTypeDetailDto>($"api/eav/metadata/composite-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateCompositeTypeAsync(
        string id, UpdateCompositeTypeRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/composite-types/{id}", request, ct);

    public Task<(bool Ok, string? Error)> DeleteCompositeTypeAsync(
        string id, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/composite-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateCompositeFieldAsync(
        string compositeTypeId, string fieldId,
        UpdateCompositeFieldRequest request, CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields/{fieldId}",
            request, ct);

    public Task<(bool Ok, string? Error)> DeleteCompositeFieldAsync(
        string compositeTypeId, string fieldId, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields/{fieldId}", ct);

    // ============================================================
    // 自定义表
    // ============================================================

    public async Task<IReadOnlyList<CustomTableDetailDto>?> ListCustomTablesAsync(
        string? entityType = null,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(entityType))
            query.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (includeDeleted) query.Add("includeDeleted=true");

        var url = "api/eav/metadata/custom-tables";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        return await GetAsync<IReadOnlyList<CustomTableDetailDto>>(url, ct);
    }

    public Task<CustomTableDetailDto?> GetCustomTableAsync(
        string id, CancellationToken ct = default)
        => GetAsync<CustomTableDetailDto>($"api/eav/metadata/custom-tables/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateCustomTableAsync(
        string id, UpdateCustomTableRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/custom-tables/{id}", request, ct);

    public Task<(bool Ok, string? Error)> DeleteCustomTableAsync(
        string id, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/custom-tables/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateTableColumnAsync(
        string tableId, string columnId,
        UpdateTableColumnRequest request, CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/custom-tables/{tableId}/columns/{columnId}",
            request, ct);

    public Task<(bool Ok, string? Error)> DeleteTableColumnAsync(
        string tableId, string columnId, CancellationToken ct = default)
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
        string id, CancellationToken ct = default)
        => GetAsync<AttributeDetailDto>($"api/eav/metadata/attributes/{id}", ct);

    public async Task<IReadOnlyList<EntityTypeSummaryDto>?> ListEntityTypesAsync(
        CancellationToken ct = default)
        => await GetAsync<IReadOnlyList<EntityTypeSummaryDto>>(
            "api/eav/entity-types", ct);

    // ============================================================
    // 实体类型 CRUD
    // ============================================================

    public Task<string?> CreateEntityTypeAsync(
        CreateEntityTypeRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/entity-types", request, "entityTypeId", ct);

    public Task<EntityTypeDetailDto?> GetEntityTypeAsync(
        string id, CancellationToken ct = default)
        => GetAsync<EntityTypeDetailDto>($"api/eav/entity-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UpdateEntityTypeAsync(
        string id, UpdateEntityTypeRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/entity-types/{id}", request, ct);

    public Task<(bool Ok, string? Error)> DeleteEntityTypeAsync(
        string id, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/entity-types/{id}", ct);

    public Task<(bool Ok, string? Error)> UndeleteEntityTypeAsync(
        string id, CancellationToken ct = default)
        => PostNoBodyAsync($"api/eav/entity-types/{id}/undelete", ct);

    public async Task<PagedResult<DynamicEntityDto>?> ListEntitiesAsync(
        string entityType, int page = 1, int pageSize = 20,
        CancellationToken ct = default)
    {
        var request = new EavQueryRequest
        {
            EntityType = entityType,
            Filters = new List<AttributeFilter>(),
            Page = page,
            PageSize = pageSize
        };
        return await PostAsync<PagedResult<DynamicEntityDto>>(
            $"api/eav/{entityType}/entities/query", request, ct);
    }

    public Task<(bool Ok, string? Error)> UpdateAttributeAsync(
        string id, UpdateAttributeRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/attributes/{id}", request, ct);

    // ============================================================
    // 选项集
    // ============================================================

    public Task<string?> CreateOptionSetAsync(
        CreateOptionSetRequest request, CancellationToken ct = default)
        => PostForIdAsync("api/eav/metadata/option-sets", request, "optionSetId", ct);

    public async Task<IReadOnlyList<OptionSetSummaryDto>?> ListOptionSetsAsync(
        string? entityType = null,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(entityType))
            query.Add($"entityType={Uri.EscapeDataString(entityType)}");
        if (includeDeleted) query.Add("includeDeleted=true");

        var url = "api/eav/metadata/option-sets";
        if (query.Count > 0) url += "?" + string.Join("&", query);

        return await GetAsync<IReadOnlyList<OptionSetSummaryDto>>(url, ct);
    }

    public Task<(bool Ok, string? Error)> DeleteOptionSetAsync(
        string optionSetId, CancellationToken ct = default)
        => DeleteWithErrorAsync($"api/eav/metadata/option-sets/{optionSetId}", ct);

    public Task<OptionSetDetailDto?> GetOptionSetAsync(
        string optionSetId,
        bool includeDeleted = false,
        CancellationToken ct = default)
    {
        var url = $"api/eav/metadata/option-sets/{optionSetId}";
        if (includeDeleted) url += "?includeDeleted=true";
        return GetAsync<OptionSetDetailDto>(url, ct);
    }

    public Task<(bool Ok, string? Error)> UpdateOptionSetAsync(
        string optionSetId, UpdateOptionSetRequest request, CancellationToken ct = default)
        => PutAsync($"api/eav/metadata/option-sets/{optionSetId}", request, ct);

    public Task<IReadOnlyList<OptionSetReferenceDto>?> GetOptionSetReferencesAsync(
        string optionSetId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<OptionSetReferenceDto>>(
            $"api/eav/metadata/option-sets/{optionSetId}/references", ct);

    // ============================================================
    // 选项项
    // ============================================================

    public Task<IReadOnlyList<OptionItemDetailDto>?> ListOptionItemsAsync(
        string optionSetId, CancellationToken ct = default)
        => GetAsync<IReadOnlyList<OptionItemDetailDto>>(
            $"api/eav/metadata/option-sets/{optionSetId}/items", ct);

    public Task<string?> AddOptionItemAsync(
        string optionSetId, CreateOptionItemRequest request,
        CancellationToken ct = default)
        => PostForIdAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items",
            request, "optionItemId", ct);

    public Task<(bool Ok, string? Error)> UpdateOptionItemAsync(
        string optionSetId, string itemId, UpdateOptionItemRequest request,
        CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/{itemId}",
            request, ct);

    public Task<(bool Ok, string? Error)> DeleteOptionItemAsync(
        string optionSetId, string itemId, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/{itemId}", ct);

    public Task<(bool Ok, string? Error)> ReorderOptionItemsAsync(
        string optionSetId, List<ReorderOptionItem> orders,
        CancellationToken ct = default)
        => PutAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/reorder",
            orders, ct);

    // ============================================================
    // undelete 端点
    // ============================================================

    public Task<(bool Ok, string? Error)> UndeleteAttributeAsync(
        string attributeId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/attributes/{attributeId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteCompositeTypeAsync(
        string compositeTypeId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteCompositeFieldAsync(
        string compositeTypeId, string fieldId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/composite-types/{compositeTypeId}/fields/{fieldId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteCustomTableAsync(
        string tableId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/custom-tables/{tableId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteTableColumnAsync(
        string tableId, string columnId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/custom-tables/{tableId}/columns/{columnId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteOptionSetAsync(
        string optionSetId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/undelete", ct);

    public Task<(bool Ok, string? Error)> UndeleteOptionItemAsync(
        string optionSetId, string itemId, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/eav/metadata/option-sets/{optionSetId}/items/{itemId}/undelete", ct);

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

    public async Task<(bool Ok, string? Error)> MigrateUnitCategoryAsync(
        Guid unitId, MigrateUnitCategoryRequest request, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"api/units/{unitId}/migrate-category", request, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = await ExtractErrorAsync(resp, ct);
                _logger.LogWarning("POST migrate-category → {Status}: {Error}",
                    resp.StatusCode, msg);
                return (false, msg);
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST migrate-category 失败");
            return (false, ex.Message);
        }
    }

    public async Task<(bool Ok, RecalculateUnitFactorResult? Result, string? Error)>
        RecalculateUnitFactorAsync(
        Guid unitId, RecalculateUnitFactorRequest request, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync(
                $"api/units/{unitId}/recalculate-factor", request, JsonOptions, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var msg = await ExtractErrorAsync(resp, ct);
                _logger.LogWarning("POST recalculate-factor → {Status}: {Error}",
                    resp.StatusCode, msg);
                return (false, null, msg);
            }

            var result = await resp.Content
                .ReadFromJsonAsync<RecalculateUnitFactorResult>(JsonOptions, ct);
            return (true, result, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST recalculate-factor 失败");
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool Ok, string? Error)> UpdateUnitAsync(
        Guid unitId, UpdateUnitRequest request, CancellationToken ct = default)
        => await PutAsync($"api/units/{unitId}", request, ct);

    public async Task<(bool Ok, string? Error)> DeleteUnitAsync(
        Guid unitId, CancellationToken ct = default)
        => await DeleteWithErrorAsync($"api/units/{unitId}", ct);

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

    private async Task<(bool Ok, string? Error)> PostNoBodyAsync(
        string relativeUrl, CancellationToken ct)
    {
        try
        {
            var resp = await _http.PostAsync(relativeUrl, content: null, ct);
            if (resp.IsSuccessStatusCode) return (true, null);

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("POST {Url} → {Status}: {Error}",
                relativeUrl, resp.StatusCode, msg);
            return (false, msg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", relativeUrl);
            return (false, ex.Message);
        }
    }

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

    /// <summary>
    /// ★ 按属性名取 ID。ID 值从 JSON 数字改为字符串。
    /// </summary>
    private async Task<string?> PostForIdAsync(
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
                && p.ValueKind == JsonValueKind.String)
                return p.GetString();

            _logger.LogWarning(
                "POST {Url} → 响应缺少或类型不符 {IdProperty}: {Body}",
                relativeUrl, idPropertyName, doc.RootElement.GetRawText());
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Url} 失败", relativeUrl);
            return null;
        }
    }

    // ============================================================
    // ★ iNode 声明层
    // ============================================================

    public async Task<IReadOnlyList<InodeTypeCardDto>?> ListInodeTypeCardsAsync(
        string inodeId, CancellationToken ct = default)
        => await GetAsync<IReadOnlyList<InodeTypeCardDto>>(
            $"api/inode/{Uri.EscapeDataString(inodeId)}/types", ct);

    public Task<(bool Ok, string? Error)> AttachInodeTypeAsync(
        string inodeId, string entityType, CancellationToken ct = default)
        => PostNoBodyAsync(
            $"api/inode/{Uri.EscapeDataString(inodeId)}/types/" +
            $"{Uri.EscapeDataString(entityType)}", ct);

    public Task<(bool Ok, string? Error)> DetachInodeTypeAsync(
        string inodeId, string entityType, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/inode/{Uri.EscapeDataString(inodeId)}/types/" +
            $"{Uri.EscapeDataString(entityType)}", ct);

    // ============================================================
    // ★ iNode 实体 CRUD
    // ============================================================

    public async Task<IReadOnlyList<DynamicEntityDto>?> ListInodeEntitiesAsync(
        string inodeId, bool originalUnits = false, CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities";
        if (originalUnits) url += "?unit=original";
        return await GetAsync<IReadOnlyList<DynamicEntityDto>>(url, ct);
    }

    public async Task<DynamicEntityDto?> GetInodeEntityAsync(
        string inodeId, string entityType,
        bool originalUnits = false, CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
                  $"{Uri.EscapeDataString(entityType)}";
        if (originalUnits) url += "?unit=original";
        return await GetAsync<DynamicEntityDto>(url, ct);
    }

    public async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        SaveInodeEntityAsync(
        string inodeId, string entityType,
        Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt = null,
        CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
                  $"{Uri.EscapeDataString(entityType)}";
        return await PutWithConflictAsync(url, values, expectedUpdatedAt, ct);
    }

    public async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        PatchInodeEntityAsync(
        string inodeId, string entityType,
        Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt = null,
        CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
                  $"{Uri.EscapeDataString(entityType)}";
        return await PatchWithConflictAsync(url, values, expectedUpdatedAt, ct);
    }

    public Task<(bool Ok, string? Error)> DeleteInodeEntityAsync(
        string inodeId, string entityType, CancellationToken ct = default)
        => DeleteWithErrorAsync(
            $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
            $"{Uri.EscapeDataString(entityType)}", ct);

    public async Task<IReadOnlyList<EntityHistoryDto>?> GetInodeEntityHistoryAsync(
        string inodeId, string entityType,
        DateTimeOffset? from = null, CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/entities/" +
                  $"{Uri.EscapeDataString(entityType)}/history";
        if (from.HasValue)
            url += $"?from={Uri.EscapeDataString(from.Value.ToString("O"))}";
        return await GetAsync<IReadOnlyList<EntityHistoryDto>>(url, ct);
    }

    // ============================================================
    // ★ 跨 iNode 查询
    // ============================================================

    public async Task<PagedResult<InodeEntityDto>?> QueryInodesAsync(
        InodeQueryRequest request, CancellationToken ct = default)
        => await PostAsync<PagedResult<InodeEntityDto>>(
            "api/inode/query", request, ct);

    // ============================================================
    // ★ iNode 全景 JSON
    // ============================================================

    /// <summary>
    /// 拉取该 iNode 下所有实体的全景 JSON（字符串形式）。
    ///
    /// 用途：外部应用 / 缓存 / 转发第三方。
    ///
    /// 参数：
    ///   displayName  - true 时属性 key 用中文 DisplayName
    ///   includeNull  - true 时保留值为 null 的属性键
    ///   units        - "default" | "base" | "original"
    /// </summary>
    public async Task<string?> GetInodeAllAsJsonAsync(
        string inodeId,
        bool displayName = false,
        bool includeNull = false,
        string? units = null,
        CancellationToken ct = default)
    {
        var url = $"api/inode/{Uri.EscapeDataString(inodeId)}/json";

        var query = new List<string>();
        if (displayName) query.Add("displayName=true");
        if (includeNull) query.Add("includeNull=true");
        if (!string.IsNullOrWhiteSpace(units))
            query.Add($"units={Uri.EscapeDataString(units)}");

        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        try
        {
            var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("GET {Url} → {Status}",
                    url, resp.StatusCode);
                return null;
            }

            // ★ 直接返回字符串，不反序列化
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Url} 失败", url);
            return null;
        }
    }

    // ============================================================
    // ★ 内部辅助：带乐观锁的 PUT / PATCH
    // ============================================================

    private async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        PutWithConflictAsync(
        string url, Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = JsonContent.Create(values, options: JsonOptions)
            };

            if (expectedUpdatedAt is { } exp)
                req.Headers.Add("X-Expected-Updated-At",
                    exp.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null, null);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PUT {Url} → 409：{Body}", url, text);

                DateTimeOffset? current = null;
                try
                {
                    var conflict = JsonSerializer.Deserialize<ConflictResponse>(
                        text, JsonOptions);
                    current = conflict?.CurrentUpdatedAt;
                }
                catch { }

                return (false, "并发冲突：实体已被其他用户修改，请刷新后重试", current);
            }

            var msg = await ExtractErrorAsync(resp, ct);
            _logger.LogWarning("PUT {Url} → {Status}: {Error}",
                url, resp.StatusCode, msg);
            return (false, msg, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT {Url} 失败", url);
            return (false, ex.Message, null);
        }
    }

    private async Task<(bool Ok, string? Error, DateTimeOffset? CurrentUpdatedAt)>
        PatchWithConflictAsync(
        string url, Dictionary<string, object?> values,
        DateTimeOffset? expectedUpdatedAt, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = JsonContent.Create(values, options: JsonOptions)
            };

            if (expectedUpdatedAt is { } exp)
                req.Headers.Add("X-Expected-Updated-At",
                    exp.ToString("O", System.Globalization.CultureInfo.InvariantCulture));

            var resp = await _http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) return (true, null, null);

            if (resp.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var text = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PATCH {Url} → 409：{Body}", url, text);

                DateTimeOffset? current = null;
                try
                {
                    var conflict = JsonSerializer.Deserialize<ConflictResponse>(
                        text, JsonOptions);
                    current = conflict?.CurrentUpdatedAt;
                }
                catch { }

                return (false, "并发冲突：实体已被其他用户修改，请刷新后重试", current);
            }

            var msg = await ExtractErrorAsync(resp, ct);
            return (false, msg, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PATCH {Url} 失败", url);
            return (false, ex.Message, null);
        }
    }

    // ============================================================
    // 私有 record
    // ============================================================

    private sealed record SchemaResponse(
        string EntityType, IReadOnlyList<AttributeSchemaDto> Attributes);

    private sealed record QueryByTableResponse(List<string> EntityIds);

    private sealed record IdResponse<T>(T Id);

    private sealed record ErrorResponse(
        string? Error, List<ValidationErrorDto>? Errors);

    private sealed record ValidationErrorDto(string Field, string Message);

    private sealed record ConflictResponse(
        string? Error,
        DateTimeOffset? CurrentUpdatedAt,
        DateTimeOffset? ExpectedUpdatedAt);
}
