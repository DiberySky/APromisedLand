using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TreeGraph.Api.Tests.Fixtures;

/// <summary>
/// 一次性测试数据初始化的静态工具。
/// 用 Lazy&lt;Task&gt; 保证多个测试类并发调用时只执行一次。
/// </summary>
public static class TestData
{
    public const string EntityType = "TestProduct";

    private static readonly Lazy<Task> _init = new(() => EnsureSchemaCoreAsync());
    private static HttpClient? _client;

    /// <summary>注入 HttpClient（由测试基类第一次调用时传入）。</summary>
    public static Task EnsureSchemaAsync(HttpClient client)
    {
        _client = client;
        return _init.Value;
    }

    private static async Task EnsureSchemaCoreAsync()
    {
        if (_client is null)
            throw new InvalidOperationException("请先传入 HttpClient");

        // ---- 属性：int / decimal / string / bool ----
        await CreateAttributeAsync("amount", "amount", "int",
            isRequired: true, isSearchable: true, isSortable: true);
        await CreateAttributeAsync("price", "price", "decimal",
            isRequired: false, isSearchable: true, isSortable: true);
        await CreateAttributeAsync("label", "label", "string",
            isRequired: false, isSearchable: true, isSortable: false);
        await CreateAttributeAsync("flag", "flag", "bool",
            isRequired: false, isSearchable: true, isSortable: false);
    }

    private static async Task CreateAttributeAsync(
        string name, string displayName, string dataType,
        bool isRequired, bool isSearchable, bool isSortable)
    {
        var body = new
        {
            entityType = EntityType,
            attributeName = name,
            displayName,
            dataType,
            isRequired,
            isSearchable,
            isSortable,
            displayOrder = 1
        };

        var resp = await _client!.PostAsJsonAsync("/api/eav/metadata/attributes", body);

        // 已存在（409）视为成功
        if (resp.StatusCode == HttpStatusCode.Conflict) return;

        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"CreateAttribute '{name}' failed {resp.StatusCode}: {text}");
        }
    }
}
