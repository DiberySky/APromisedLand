using System.ComponentModel.DataAnnotations;

namespace MAFWorkFlowApi.Models;

public sealed class NodeUpsertRequest
{
    public Guid? Guid { get; set; }

    [Required, StringLength(100, MinimumLength = 1)]
    [RegularExpression(@"^[^/]+$",
        ErrorMessage = "简称不能包含 '/'")]
    public string DisplayName { get; set; } = string.Empty;

    public Guid? ParentGuid { get; set; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string Type { get; set; } = string.Empty;

    public List<string>? ExtraLabels { get; set; }
    public Dictionary<string, object?> Attributes { get; set; } = new();
}