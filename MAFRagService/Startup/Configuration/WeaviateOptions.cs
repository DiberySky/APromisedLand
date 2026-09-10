using System.ComponentModel.DataAnnotations;

namespace MAFRagService.Startup.Configuration;

public sealed class WeaviateOptions
{
    public const string SectionName = "Weaviate";

    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    [Range(0, 5)]
    public int RetryCount { get; set; } = 2;
}