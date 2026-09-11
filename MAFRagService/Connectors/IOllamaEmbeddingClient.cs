namespace MAFRagService.Connectors;

public interface IOllamaEmbeddingClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}