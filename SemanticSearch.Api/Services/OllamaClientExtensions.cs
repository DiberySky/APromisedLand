using OllamaSharp;

namespace SemanticSearch.Api.Services;

public static class OllamaClientExtensions
{
    public static void AddOllamaApiClient(
        this IHostApplicationBuilder builder)
    {
        var endpoint = builder.Configuration["OLLAMA_URI"];
        if (endpoint is null)
            throw new Exception($"OLLAMA_URI 对应的配置缺失。");

        try
        {
            builder.Services.AddSingleton<IOllamaApiClient>(_ =>
                new OllamaApiClient(new Uri(endpoint)));
        }
        catch (Exception e)
        {
            throw new InvalidOperationException(e.Message, e);
        }
    }
}