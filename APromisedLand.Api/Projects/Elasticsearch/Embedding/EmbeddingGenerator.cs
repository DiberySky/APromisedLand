using OllamaSharp;
using OllamaSharp.Models;

namespace APromisedLand.Api.Projects.Elasticsearch.Embedding;

public class EmbeddingGenerator(IOllamaApiClient ollama) : IEmbeddingGenerator
{
    public async Task<float[]> GenerateAsync(string text)
    {
        var response = await ollama.EmbedAsync(new EmbedRequest
        {
            Model = "bge-large",
            Input = [ text ]
        });
        
        return response.Embeddings.FirstOrDefault() ?? [];
    }
}