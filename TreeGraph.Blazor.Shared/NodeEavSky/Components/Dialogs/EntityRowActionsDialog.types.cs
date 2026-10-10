namespace TreeGraph.Blazor.Shared.NodeEavSky.Components.Dialogs;

/// <summary>实体行操作类型。调用方根据返回值决定后续行为。</summary>
public enum EntityRowAction
{
    Edit,
    CopyId,
    History,
    Delete
}

/// <summary>只读摘要行（标签 + 值）。</summary>
public record SummaryLine(string Label, string Value);
