using OllamaSharp;
using OllamaSharp.Models;

namespace ElasticsearchService.Embeds;

public class EmbeddingService : IEmbeddingService
{
    private readonly IOllamaApiClient _ollama;

    public EmbeddingService(IOllamaApiClient ollama)
    {
        _ollama = ollama;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var response = await _ollama.EmbedAsync(new EmbedRequest
        {
            Model = "bge-large",
            Input = [ text ]
        });
        return response.Embeddings.FirstOrDefault() ?? Array.Empty<float>();
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        var response = await _ollama.EmbedAsync(new EmbedRequest
        {
            Model = "bge-large",
            Input = texts
        });
        return response.Embeddings;
    }
}