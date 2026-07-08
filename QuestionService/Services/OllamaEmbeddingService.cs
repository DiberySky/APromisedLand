using OllamaSharp;
using OllamaSharp.Models;

namespace QuestionService.Services;

public class OllamaEmbeddingService(IOllamaApiClient ollama) : IOllamaEmbeddingService
{
    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        var response = await ollama.EmbedAsync(new EmbedRequest
        {
            Model = "bge-large",
            Input = [ text ]
        });
        
        return response.Embeddings.FirstOrDefault() ?? Array.Empty<float>();
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        var response = await ollama.EmbedAsync(new EmbedRequest
        {
            Model = "bge-large",
            Input = texts
        });
        
        return response.Embeddings;
    }
}