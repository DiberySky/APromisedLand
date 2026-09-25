using DiberyBlazorWebSky.Models.Graph;
using DiberyBlazorWebSky.Services;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;

/// <summary>
/// <see cref="IGraphPageContext"/> 的 Scoped 实现，跨页面保持选中图。
/// </summary>
public class GraphPageState : IGraphPageContext
{
    private readonly GraphAdminApiClient _graphApi;
    private readonly ILogger<GraphPageState> _logger;

    private List<GraphDto> _graphs = new();
    private string? _selectedGraphGuid;
    private bool _isLoading;
    private string? _errorMessage;

    public IReadOnlyList<GraphDto> Graphs => _graphs;
    public string? SelectedGraphGuid => _selectedGraphGuid;
    public bool IsLoading => _isLoading;
    public string? ErrorMessage => _errorMessage;

    public event Func<Task>? StateChanged;

    public GraphPageState(GraphAdminApiClient graphApi, ILogger<GraphPageState> logger)
    {
        _graphApi = graphApi;
        _logger = logger;
    }

    public async Task LoadGraphsAsync()
    {
        _isLoading = true;
        _errorMessage = null;
        await NotifyStateChangedAsync();

        try
        {
            _graphs = await _graphApi.ListGraphsAsync();
            _logger.LogInformation("加载 {Count} 个图", _graphs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载图列表失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isLoading = false;
            await NotifyStateChangedAsync();
        }
    }

    public async Task SelectGraphAsync(string? guid)
    {
        _selectedGraphGuid = guid;
        await NotifyStateChangedAsync();
    }

    public Task RefreshAsync() => LoadGraphsAsync();

    public void SetError(string? message)
    {
        _errorMessage = message;
        _ = NotifyStateChangedAsync();
    }

    public void ClearError()
    {
        _errorMessage = null;
        _ = NotifyStateChangedAsync();
    }

    private async Task NotifyStateChangedAsync()
    {
        if (StateChanged is not null)
        {
            await StateChanged.Invoke();
        }
    }
}
