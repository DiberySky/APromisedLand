// /Configuration/OllamaOptions.cs

using System.ComponentModel.DataAnnotations;

namespace MAFRagService.Startup.Configuration;

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    /// <summary>允许为空——由连接串解析后 PostConfigure 覆盖。</summary>
    [Url]
    public string Url { get; set; } = "";

    [Required, MinLength(1)]
    public string ChatModel { get; set; } = "qwen2.5:7b"; //qwen2.5:7b 、qwen3.8:27b

    [Required, MinLength(1)]
    public string EmbeddingModel { get; set; } = "bge-large";

    [Range(typeof(TimeSpan), "00:00:10", "01:00:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);

    [Range(typeof(TimeSpan), "00:00:05", "00:30:00")]
    public TimeSpan PolicyTimeout { get; set; } = TimeSpan.FromMinutes(3);

    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;
}