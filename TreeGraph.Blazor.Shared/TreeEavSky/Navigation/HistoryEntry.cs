namespace TreeGraph.Blazor.Shared.TreeEavSky.Navigation;

public class HistoryEntry
{
    public string Url { get; set; } = string.Empty;
    public string? RootId { get; set; }
    public string? ClickNodeId { get; set; }
}
