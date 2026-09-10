using System.Text.Json;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Connectors;

public class OllamaModelConnector : IAgentModel
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaModelConnector(HttpClient httpClient, string baseUrl, string model)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var request = new
        {
            model = _model,
            prompt = prompt,
            stream = false
        };
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/generate", request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<OllamaResponse>(json);
        return result?.Response ?? string.Empty;
    }

    private class OllamaResponse { public string Response { get; set; } = string.Empty; }
}
