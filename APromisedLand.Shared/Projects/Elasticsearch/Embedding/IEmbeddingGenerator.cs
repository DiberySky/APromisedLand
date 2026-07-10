namespace APromisedLand.Shared.Projects.Elasticsearch.Embedding;

public interface IEmbeddingGenerator
{
    Task<float[]> GenerateAsync(string text);
}