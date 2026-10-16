using System.ComponentModel.DataAnnotations;

namespace TreeGraph.MafWfApi.Models;

/// <summary>POST /api/naming/generate 请求体。</summary>
public sealed class NamingRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public string Text { get; set; } = string.Empty;
}

/// <summary>POST /api/naming/generate 响应体。</summary>
public sealed record NamingResult(
    string Source,
    string SnakeCase,
    string PascalCase,
    IReadOnlyList<string> Warnings);
