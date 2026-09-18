using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Models;

public sealed class EdgeUpsertRequest
{
    public Guid? Guid { get; set; }

    [Required]
    public Guid From { get; set; }

    [Required]
    public Guid To { get; set; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string Type { get; set; } = string.Empty;

    public bool Undirected { get; set; }
    public double? Cost { get; set; }
    public Dictionary<string, object?> Attributes { get; set; } = new();
}