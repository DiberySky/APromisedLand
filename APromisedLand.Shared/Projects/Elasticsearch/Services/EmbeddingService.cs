using OllamaSharp;
using OllamaSharp.Models;

namespace APromisedLand.Shared.Projects.Elasticsearch;

public class EmbeddingService
{
    private readonly IOllamaApiClient _ollama;
    private const string ModelName = "bge-large";

    public EmbeddingService(IOllamaApiClient ollama)
    {
        _ollama = ollama;
        ollama.SelectedModel = ModelName;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var response = await _ollama.EmbedAsync(new EmbedRequest
        {
            Model = ModelName, //"bge-large",
            Input = [ text ]
        });

        return response.Embeddings.FirstOrDefault() ?? [];
    }
}