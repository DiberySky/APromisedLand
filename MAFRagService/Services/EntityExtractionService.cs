using System.Text.Json;
using MAFRagService.Models;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Services;

public interface IEntityExtractionService
{
    Task<List<EntityInfo>> ExtractAsync(string text, string tenant, CancellationToken ct = default);
}

/// <summary>
/// 通过 IAgentModel 调用 LLM 抽取实体。
/// 不再直接读 IConfiguration["Ollama:Url"] —— 模型名 / 端点由 AddRagAiServices 统一解析后
/// 通过 OllamaOptions + 命名 HttpClient 注入。
/// </summary>
public class EntityExtractionService : IEntityExtractionService
{
    private readonly IAgentModel _model;
    private readonly ILogger<EntityExtractionService> _logger;

    public EntityExtractionService(
        IAgentModel model,
        ILogger<EntityExtractionService> logger)
    {
        _model  = model;
        _logger = logger;
    }

    public async Task<List<EntityInfo>> ExtractAsync(
        string text, string tenant, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();

        var prompt = $@"
从以下文本中提取关键实体（人物、组织、技术、地点等）。
只输出 JSON 数组，不要任何解释或代码块围栏。
格式：[{{""name"": ""实体名"", ""type"": ""类型""}}]
文本：{text}
";

        try
        {
            var response = await _model.GenerateAsync(prompt, ct);
            return ParseEntities(response);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Entity extraction failed");
            return new();
        }
    }

    private List<EntityInfo> ParseEntities(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return new();

        var json = ExtractJsonArray(response);
        if (json is null) return new();

        try
        {
            return JsonSerializer.Deserialize<List<EntityInfo>>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? new();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Failed to parse entity extraction result: {Error}", ex.Message);
            return new();
        }
    }

    /// <summary>容忍 LLM 输出被 ```json ... ``` 围栏包裹。</summary>
    private static string? ExtractJsonArray(string raw)
    {
        var start = raw.IndexOf('[');
        var end   = raw.LastIndexOf(']');
        if (start < 0 || end <= start) return null;
        return raw.Substring(start, end - start + 1);
    }
}