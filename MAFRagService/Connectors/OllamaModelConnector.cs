using System.Text.Json;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Connectors;

public class OllamaModelConnector(HttpClient httpClient, string baseUrl, string model) : IAgentModel
{
    private readonly string _baseUrl = baseUrl.TrimEnd('/');

    public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
    {
        var request = new
        {
            model = model,
            prompt = prompt,
            stream = false
        };
        var response = await httpClient.PostAsJsonAsync($"{_baseUrl}/api/generate", request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<OllamaResponse>(json);
        return result?.Response ?? string.Empty;
    }

    private class OllamaResponse { public string Response { get; set; } = string.Empty; }
}
