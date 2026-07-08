using OllamaSharp;
using OllamaSharp.Models;

namespace SemanticSearch.Api.Services;

public class EmbeddingService
{
    private IOllamaApiClient _ollama;
    private readonly string _modelName = "bge-large";

    public EmbeddingService(IOllamaApiClient ollama)
    {
        _ollama = ollama;
        ollama.SelectedModel = _modelName;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var response = await _ollama.EmbedAsync(new EmbedRequest
        {
            Model = "bge-large",
            Input = [ text ]
        });

        return response.Embeddings.FirstOrDefault() ?? [];
    }
}