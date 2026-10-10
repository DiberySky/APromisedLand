namespace TreeGraph.Blazor.Shared.TreeEavSky.Navigation;

public interface ITreeNavigationHistoryService
{
    void Push(string url, string? rootId = null, string? clickNodeId = null);
    HistoryEntry? Pop();
    HistoryEntry? Peek();
    HistoryEntry? PopReturnUrlOrDefault(string defaultUrl);
    void Clear();
    bool CanGoBack { get; }
    IReadOnlyCollection<HistoryEntry> Stack { get; }
}
