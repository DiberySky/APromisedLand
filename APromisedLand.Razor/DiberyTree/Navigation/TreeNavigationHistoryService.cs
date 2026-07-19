using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace APromisedLand.Razor.DiberyTree.Navigation;

public class TreeNavigationHistoryService : ITreeNavigationHistoryService
{
    private readonly Stack<HistoryEntry> _stack = new();

    public IReadOnlyCollection<HistoryEntry> Stack => _stack.ToList().AsReadOnly();
    public bool CanGoBack => _stack.Count > 0;

    /// <summary>
    /// 打开新页前调用，保存当前位置
    /// </summary>
    public void Push(string url, string? rootId = null, string? clickNodeId = null)
    {
        var entry = new HistoryEntry
        {
            Url = url,
            RootId = rootId,
            ClickNodeId = clickNodeId,
        };
        
        _stack.Push(entry);
    }

    /// <summary>
    /// 返回上一页（弹出并返回）
    /// </summary>
    public HistoryEntry? Pop()
    {
        if (_stack.Count == 0) return null;
        
        var entry = _stack.Pop();
        return entry;
    }

    public HistoryEntry? Peek() => _stack.Count > 0 ? _stack.Peek() : null;

    /// <summary>
    /// 弹出并导航返回，或跳转到默认页
    /// </summary>
    public HistoryEntry? PopReturnUrlOrDefault(string defaultUrl)
    {
        var entry = Pop();
        if (entry != null)
        {
            // navigation.NavigateTo(entry.Url, forceLoad: true);
            return entry;
        }
        
        // navigation.NavigateTo(defaultUrl, forceLoad: true);
        return null;
    }

    public void Clear() => _stack.Clear();
}