using System.Text.Json;
using MAFRagService.Models;

namespace MAFRagService.Services;

public interface IEntityExtractionService
{
    Task<List<EntityInfo>> ExtractAsync(string text, string tenant, CancellationToken ct = default);
}

public class EntityExtractionService : IEntityExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<EntityExtractionService> _logger;

    public EntityExtractionService(HttpClient httpClient, IConfiguration config, ILogger<EntityExtractionService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<List<EntityInfo>> ExtractAsync(string text, string tenant, CancellationToken ct = default)
    {
        var ollamaUrl = _config["Ollama:Url"] ?? "http://localhost:11434";
        var model = _config["Ollama:Model"] ?? "llama2";

        var prompt = $@"
从以下文本中提取关键实体（人物、组织、技术、地点等）。
输出 JSON 数组格式：
[{{""name"": ""实体名"", ""type"": ""类型""}}]
文本：{text}
";
        var request = new { model, prompt, stream = false };
        var response = await _httpClient.PostAsJsonAsync($"{ollamaUrl}/api/generate", request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("response").GetString() ?? "";
            return JsonSerializer.Deserialize<List<EntityInfo>>(content) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to parse entity extraction result: {Error}", ex.Message);
            return new();
        }
    }
}
