using System.Net;
using System.Net.Http.Json;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// iNode 测试用的 schema 初始化辅助。
///
/// 每个 entityType 只初始化一次（用 static 字典 + 锁保护）。
/// 注意两步：先建实体类型（/api/eav/entity-types），
/// 再建属性（/api/eav/metadata/attributes），否则 CreateAttribute 400。
/// </summary>
public static class InodeTestData
{
    private static readonly Dictionary<string, Task> _initByType = new();
    private static readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>确保 entityType 已注册（实体类型 + 3 个属性）。</summary>
    public static async Task EnsureTypeAsync(HttpClient client, string entityType)
    {
        Task task;
        await _lock.WaitAsync();
        try
        {
            if (_initByType.TryGetValue(entityType, out var existing))
            {
                task = existing;
            }
            else
            {
                task = CreateTypeAsync(client, entityType);
                _initByType[entityType] = task;
            }
        }
        finally
        {
            _lock.Release();
        }

        // 在锁外 await，避免串行阻塞
        await task;
    }

    private static async Task CreateTypeAsync(HttpClient client, string entityType)
    {
        // ---- 先创建实体类型（否则 CreateAttribute 400）----
        var typeBody = new { entityType, displayName = entityType };
        var typeResp = await client.PostAsJsonAsync(
            "/api/eav/entity-types", typeBody);

        // 409 = 已存在，视为成功
        if (!typeResp.IsSuccessStatusCode
            && typeResp.StatusCode != HttpStatusCode.Conflict)
        {
            var text = await typeResp.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Create entity type '{entityType}' failed " +
                $"{typeResp.StatusCode}: {text}");
        }

        // ---- 3 个属性：string / int / decimal ----
        await CreateAttrAsync(client, entityType, "name", "string",
            isRequired: false, isSearchable: true, isSortable: false, displayOrder: 1);
        await CreateAttrAsync(client, entityType, "amount", "int",
            isRequired: false, isSearchable: true, isSortable: true, displayOrder: 2);
        await CreateAttrAsync(client, entityType, "price", "decimal",
            isRequired: false, isSearchable: true, isSortable: true, displayOrder: 3);
    }

    private static async Task CreateAttrAsync(
        HttpClient client, string entityType, string attrName, string dataType,
        bool isRequired, bool isSearchable, bool isSortable, int displayOrder)
    {
        var body = new
        {
            entityType,
            attributeName = attrName,
            displayName = attrName,
            dataType,
            isRequired,
            isSearchable,
            isSortable,
            displayOrder
        };

        var resp = await client.PostAsJsonAsync(
            "/api/eav/metadata/attributes", body);

        // 409 = 已存在（其他测试已建过），视为成功
        if (resp.StatusCode == HttpStatusCode.Conflict) return;
        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Create attribute '{entityType}.{attrName}' failed " +
                $"{resp.StatusCode}: {text}");
        }
    }
}
