using APromisedLand.Shared.Interfaces;
using APromisedLand.Shared.Projects.Elasticsearch;
using APromisedLand.Shared.Projects.Elasticsearch.Embedding;
using OllamaSharp;
using OllamaSharp.Models;

namespace QuestionService.Services;

public class EmbeddingGenerator(IOllamaApiClient ollama) : IEmbeddingGenerator
{
    public async Task<float[]> GenerateAsync(string text)
    {
        var response = await ollama.EmbedAsync(new EmbedRequest
        {
            Model = "bge-large",
            Input = [ text ]
        });
        
        return response.Embeddings.FirstOrDefault() ?? Array.Empty<float>();
    }
}