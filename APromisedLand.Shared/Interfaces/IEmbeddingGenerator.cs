namespace APromisedLand.Shared.Interfaces;

public interface IEmbeddingGenerator
{
    Task<float[]> GenerateAsync(string text);
}