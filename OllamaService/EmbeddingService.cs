using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaSharp.Models;

namespace OllamaService;

public class EmbeddingService(IOllamaApiClient ollama)
{
    public async Task<List<float[]>> GenerateEmbedding(List<string> textList)
    {
        var request = new EmbedRequest
        {
            Model = "bge-large",
            Input = textList //["需要生成向量的文本", "为这个句子生成表示以用于检索相关文章："]
        };

        var response = await ollama.EmbedAsync(request);

        return response.Embeddings;
    }
}