using System.Text.Json;
using System.Text.Json.Serialization;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Connectors;

public class OllamaModelConnector(HttpClient httpClient, string baseUrl, string model) : IAgentModel
{
    private readonly string _baseUrl = baseUrl.TrimEnd('/');

    /// <summary>
    /// Ollama 返回的字段名是小写（response / done / model），
    /// C# 属性名是 PascalCase。开启 PropertyNameCaseInsensitive 才能正确映射。
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var request = new
        {
            model  = model,
            prompt = prompt,
            stream = false
        };

        using var response = await httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/generate", request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<OllamaResponse>(json, JsonOptions);

        return result?.Response ?? string.Empty;
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;

        [JsonPropertyName("done")]
        public bool Done { get; set; }
    }
}