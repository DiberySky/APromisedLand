using System.Text.Encodings.Web;
using System.Text.Json;
using DiberyBlazorWebSky.Models.Graph;
using DiberyBlazorWebSky.Services;
using DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Web;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce;

public partial class GraphEdges : ComponentBase, IDisposable
{
    [Inject] private IGraphPageContext Context { get; set; } = default!;
    [Inject] private GraphAdminApiClient GraphApi { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<GraphEdges> Logger { get; set; } = default!;

    private List<NodeDto> _nodes = new();
    private List<EdgeDto> _edges = new();

    private bool _isBusy;
    private string? _errorMessage;
    private string? _detailJson;

    // 分页
    private Guid? _edgeContinuationToken;
    private long _edgeTotal;
    private bool _edgeHasPrev;
    private bool _edgeHasNext;

    private const int PageSize = 20;

    // CRUD 输入
    private string _newEdgeFrom = "";
    private string _newEdgeTo = "";
    private string _newEdgeName = "";

    // 行内编辑
    private Guid? _editingEdgeGuid;
    private string _editingEdgeName = "";

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    protected override async Task OnInitializedAsync()
    {
        Context.StateChanged += OnContextStateChanged;
        if (Context.Graphs.Count == 0)
        {
            await Context.LoadGraphsAsync();
        }
        if (!string.IsNullOrEmpty(Context.SelectedGraphGuid))
        {
            await LoadDataAsync();
        }
    }

    private async Task OnContextStateChanged()
    {
        _editingEdgeGuid = null;
        _editingEdgeName = "";
        _newEdgeFrom = _newEdgeTo = _newEdgeName = "";
        await LoadDataAsync();
        StateHasChanged();
    }

    private async Task LoadDataAsync()
    {
        await Task.WhenAll(LoadNodesAsync(), LoadEdgesAsync());
    }

    private async Task LoadNodesAsync()
    {
        if (string.IsNullOrEmpty(Context.SelectedGraphGuid))
        {
            _nodes.Clear();
            return;
        }

        var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);
        try
        {
            _nodes = await GraphApi.ListAllNodesAsync(graphGuid);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载节点列表失败");
            _errorMessage = ex.Message;
        }
    }

    // ─── 边查询 ──────────────────────────────────────
    private async Task LoadEdgesAsync() => await LoadEdgesAsync(null);

    private async Task LoadEdgesAsync(Guid? continuationToken)
    {
        if (string.IsNullOrEmpty(Context.SelectedGraphGuid))
        {
            _edges.Clear();
            _edgeTotal = 0;
            _edgeHasPrev = _edgeHasNext = false;
            return;
        }

        var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);
        _isBusy = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var result = await GraphApi.EnumerateEdgesAsync(graphGuid, PageSize, continuationToken);
            _edges = result.Objects;
            _edgeTotal = result.TotalRecords;
            _edgeContinuationToken = result.ContinuationToken;
            _edgeHasPrev = continuationToken != null;
            _edgeHasNext = result.ContinuationToken != null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载边失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task NextEdgePageAsync()
    {
        if (_edgeHasNext) await LoadEdgesAsync(_edgeContinuationToken);
    }

    private async Task PrevEdgePageAsync()
    {
        _edgeContinuationToken = null;
        await LoadEdgesAsync(null);
    }

    // ─── CRUD 边 ─────────────────────────────────────
    private async Task CreateEdgeAsync()
    {
        if (string.IsNullOrWhiteSpace(_newEdgeFrom) ||
            string.IsNullOrWhiteSpace(_newEdgeTo) ||
            string.IsNullOrEmpty(Context.SelectedGraphGuid))
            return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);
            var edgeName = string.IsNullOrWhiteSpace(_newEdgeName) ? "RELATED_TO" : _newEdgeName.Trim();

            var ok = await GraphApi.CreateEdgeAsync(
                graphGuid, Guid.Parse(_newEdgeFrom), Guid.Parse(_newEdgeTo), edgeName);

            if (ok)
            {
                _newEdgeFrom = _newEdgeTo = _newEdgeName = "";
                await LoadEdgesAsync();
            }
            else
            {
                _errorMessage = "创建边失败";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "创建边失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task DeleteEdgeAsync(Guid edgeGuid)
    {
        if (!await ConfirmAsync("确定删除该边？")) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);
            await GraphApi.DeleteEdgeAsync(graphGuid, edgeGuid);
            await LoadEdgesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "删除边失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task SaveEdgeNameAsync(EdgeDto edge)
    {
        if (_editingEdgeGuid != edge.Guid) return;

        var newName = _editingEdgeName.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName == edge.Name)
        {
            CancelEditEdge();
            return;
        }

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);
            var ok = await GraphApi.UpdateEdgeAsync(graphGuid, edge.Guid, newName);
            if (ok)
            {
                CancelEditEdge();
                await LoadEdgesAsync();
            }
            else
            {
                _errorMessage = "重命名失败";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "重命名边失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    // ─── UI 辅助 ─────────────────────────────────────
    private void ShowEdgeJson(EdgeDto edge) =>
        _detailJson = JsonSerializer.Serialize(edge, PrettyJson);

    private void CloseDetailModal()
    {
        _detailJson = null;
    }

    private void StartEditEdge(EdgeDto edge)
    {
        _editingEdgeGuid = edge.Guid;
        _editingEdgeName = edge.Name;
    }

    private void CancelEditEdge()
    {
        _editingEdgeGuid = null;
        _editingEdgeName = "";
    }

    private async Task HandleEditKeyDown(KeyboardEventArgs e, Func<Task> onSave)
    {
        if (e.Key == "Enter") await onSave();
        else if (e.Key == "Escape") CancelEditEdge();
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        try { return await Js.InvokeAsync<bool>("confirm", message); }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "JS confirm 调用失败，默认允许操作");
            return true;
        }
    }

    public void Dispose()
    {
        Context.StateChanged -= OnContextStateChanged;
    }
}
