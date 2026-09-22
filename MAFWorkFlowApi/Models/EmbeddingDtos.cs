using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Models;

/// <summary>POST /api/embedding/embed 请求体。</summary>
public sealed class EmbedRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Text { get; set; } = "";
}

/// <summary>POST /api/embedding/embed 响应体。</summary>
public sealed record EmbedResponse(
    List<float> Vector,
    int Dimension,
    string Model);