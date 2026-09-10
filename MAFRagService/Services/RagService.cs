using System.Text.Json;
using MAFRagService.Models;

namespace MAFRagService.Services;

public class RagService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<RagService> _logger;

    public RagService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<RagService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<List<Source>> SearchAsync(string query, string tenant, int topK, string version, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("WeaviateClient");
        var url = $"{_config["Weaviate:Url"]}/v1/graphql";

        var graphqlQuery = new
        {
            query = $@"
            {{
                Get {{
                    Document(
                        nearText: {{ concepts: [""{query}""] }},
                        where: {{ operator: And, operands: [
                            {{ path: [""tenant""], operator: Equal, valueString: ""{tenant}"" }},
                            {{ path: [""version""], operator: Equal, valueString: ""{version}"" }}
                        ]}},
                        limit: {topK}
                    ) {{
                        docId
                        content
                        chunkIndex
                        _additional {{ distance }}
                    }}
                }}
            }}"
        };

        var response = await client.PostAsJsonAsync(url, graphqlQuery, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("Weaviate response: {Json}", json);

        // 简化解析：实际应使用 JsonDocument 提取
        var sources = new List<Source>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data").GetProperty("Get").GetProperty("Document");
            foreach (var item in data.EnumerateArray())
            {
                sources.Add(new Source
                {
                    DocId = item.GetProperty("docId").GetString() ?? "",
                    Content = item.GetProperty("content").GetString() ?? "",
                    Score = 1.0f - (float)(item.GetProperty("_additional").GetProperty("distance").GetDouble())
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to parse Weaviate response: {Error}", ex.Message);
        }
        return sources;
    }
}
