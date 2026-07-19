namespace APromisedLand.Razor.DiberyTree.Navigation;

public class HistoryEntry
{
    public string Url { get; set; } = string.Empty;
    public string? RootId { get; set; }
    public string? ClickNodeId { get; set; }

    // public Dictionary<string, object?> State { get; set; } = new();
    // public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now; 
}