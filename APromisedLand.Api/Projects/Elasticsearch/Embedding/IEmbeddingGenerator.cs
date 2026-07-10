namespace APromisedLand.Api.Projects.Elasticsearch.Embedding;

public interface IEmbeddingGenerator
{
    Task<float[]> GenerateAsync(string text);
}