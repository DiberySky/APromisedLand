using System.Text;
using System.Text.Json;

namespace MAFRagService.Initializers;

public class WeaviateSchemaInitializer
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WeaviateSchemaInitializer> _logger;

    // ✅ 删除 _config 字段：地址来源统一由 WeaviateClient 的 BaseAddress 提供

    private const string SchemaPath = "/v1/schema";
    private const string ClassName  = "Document";

    public WeaviateSchemaInitializer(
        IHttpClientFactory httpClientFactory,
        ILogger<WeaviateSchemaInitializer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("WeaviateClient");

        // ✅ 打印实际目标地址，方便排查（再也不会出现"以为连的是 A，其实连的是 B"）
        _logger.LogInformation("Weaviate base address = {Url}", client.BaseAddress);

        // ---- 1. 检查 class 是否已存在 ----
        using (var checkResp = await client.GetAsync($"{SchemaPath}/{ClassName}", ct))
        {
            if (checkResp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Weaviate class '{Class}' already exists", ClassName);
                return;
            }

            // 404 属于正常（class 不存在，待创建）；其它错误一并抛出，避免"静默失败后 Post 覆盖真实错误"
            if (checkResp.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var body = await checkResp.Content.ReadAsStringAsync(ct);
                throw new Exception(
                    $"Weaviate schema check failed: {(int)checkResp.StatusCode} {checkResp.ReasonPhrase}. Body: {body}");
            }
        }

        // ---- 2. 创建 schema ----
        var schema = new
        {
            @class = ClassName,
            vectorizer = "none",
            properties = new[]
            {
                new { name = "docId",      dataType = new[] { "string" } },
                new { name = "tenant",     dataType = new[] { "string" } },
                new { name = "version",    dataType = new[] { "string" } },
                new { name = "chunkIndex", dataType = new[] { "int" } },
                new { name = "content",    dataType = new[] { "text" } },
                new { name = "filePath",   dataType = new[] { "string" } }
            }
        };

        var json = JsonSerializer.Serialize(schema);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(SchemaPath, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new Exception(
                $"Weaviate schema init failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {error}");
        }

        _logger.LogInformation("Weaviate schema initialized (class='{Class}')", ClassName);
    }
}