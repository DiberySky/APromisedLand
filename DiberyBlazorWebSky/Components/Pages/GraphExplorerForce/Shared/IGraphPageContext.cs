using DiberyBlazorWebSky.Models.Graph;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;

/// <summary>
/// 图资源管理器各页面之间共享的状态接口。
/// 负责跨页面保持「图列表」与「当前选中图」，并在状态变化时通知订阅者。
/// </summary>
public interface IGraphPageContext
{
    /// <summary>当前可用的图列表。</summary>
    IReadOnlyList<GraphDto> Graphs { get; }

    /// <summary>当前选中的图 Guid（字符串形式），未选中时为 null。</summary>
    string? SelectedGraphGuid { get; }

    /// <summary>是否正在加载图列表。</summary>
    bool IsLoading { get; }

    /// <summary>最近一次错误信息。</summary>
    string? ErrorMessage { get; }

    /// <summary>状态变化事件，页面可订阅以刷新自身数据。</summary>
    event Func<Task>? StateChanged;

    /// <summary>从后端加载图列表。</summary>
    Task LoadGraphsAsync();

    /// <summary>切换当前选中的图。</summary>
    Task SelectGraphAsync(string? guid);

    /// <summary>刷新图列表（管理弹窗增删改后调用）。</summary>
    Task RefreshAsync();

    /// <summary>设置错误信息。</summary>
    void SetError(string? message);

    /// <summary>清除错误信息。</summary>
    void ClearError();
}
