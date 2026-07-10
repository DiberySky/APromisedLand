namespace QuestionService.Services;

public interface IEmbeddingGenerator
{
    Task<float[]> GenerateAsync(string text);
}