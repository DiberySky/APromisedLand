using System.ComponentModel.DataAnnotations;

namespace TreeGraph.Shared.MafWfSky.Contracts;

/// <summary>POST /api/chat 请求体。</summary>
public sealed class ChatRequestDto
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

/// <summary>POST /api/chat 响应体。</summary>
public sealed record ChatResponseDto(string Reply, string Model);
