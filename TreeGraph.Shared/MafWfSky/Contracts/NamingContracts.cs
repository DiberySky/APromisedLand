using System.Text.Json.Serialization;

namespace TreeGraph.Shared.MafWfSky.Contracts;

/// <summary>命名生成请求（POST /api/naming/generate）。</summary>
public sealed class NamingRequestDto
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// 命名生成结果（镜像 TreeGraph.MafWfApi 的 NamingResult）。
/// snakeCase 与 pascalCase 由后端代码侧确定性规则产出，LLM 仅负责翻译。
/// </summary>
public sealed class NamingResultDto
{
    [JsonPropertyName("snakeCase")]
    public string SnakeCase { get; set; } = string.Empty;

    [JsonPropertyName("pascalCase")]
    public string PascalCase { get; set; } = string.Empty;

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];
}
