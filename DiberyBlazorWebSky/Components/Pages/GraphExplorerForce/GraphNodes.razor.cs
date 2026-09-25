using System.Text.Encodings.Web;
using System.Text.Json;
using DiberyBlazorWebSky.Models.Graph;
using DiberyBlazorWebSky.Services;
using DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Web;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce;

public partial class GraphNodes : ComponentBase, IDisposable
{
    [Inject] private IGraphPageContext Context { get; set; } = default!;
    [Inject] private GraphAdminApiClient GraphApi { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<GraphNodes> Logger { get; set; } = default!;

    private List<NodeDto> _nodes = new();

    private bool _isBusy;
    private string? _errorMessage;
    private string? _detailJson;

    // 分页
    private Guid? _nodeContinuationToken;
    private long _nodeTotal;
    private bool _nodeHasPrev;
    private bool _nodeHasNext;

    private const int PageSize = 20;

    // CRUD 输入
    private string _newNodeName = "";
    private readonly HashSet<Guid> _selectedNodeGuids = new();

    // 行内编辑
    private Guid? _editingNodeGuid;
    private string _editingNodeName = "";

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
            await LoadNodesAsync();
        }
    }

    private async Task OnContextStateChanged()
    {
        // 选中图变化时重新加载节点
        _selectedNodeGuids.Clear();
        _editingNodeGuid = null;
        _editingNodeName = "";
        await LoadNodesAsync();
        StateHasChanged();
    }

    // ─── 节点查询 ─────────────────────────────────────
    private async Task LoadNodesAsync() => await LoadNodesAsync(null);

    private async Task LoadNodesAsync(Guid? continuationToken)
    {
        if (string.IsNullOrEmpty(Context.SelectedGraphGuid))
        {
            _nodes.Clear();
            _nodeTotal = 0;
            _nodeHasPrev = _nodeHasNext = false;
            return;
        }

        var graphGuid = Guid.Parse(Context.SelectedGraphGuid);
        _isBusy = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var result = await GraphApi.EnumerateNodesAsync(graphGuid, PageSize, continuationToken);
            _nodes = result.Objects;
            _nodeTotal = result.TotalRecords;
            _nodeContinuationToken = result.ContinuationToken;
            _nodeHasPrev = continuationToken != null;
            _nodeHasNext = result.ContinuationToken != null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "加载节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task NextNodePageAsync()
    {
        if (_nodeHasNext) await LoadNodesAsync(_nodeContinuationToken);
    }

    private async Task PrevNodePageAsync()
    {
        _nodeContinuationToken = null;
        await LoadNodesAsync(null);
    }

    // ─── CRUD 节点 ───────────────────────────────────
    private async Task CreateNodeAsync()
    {
        if (string.IsNullOrWhiteSpace(_newNodeName) || string.IsNullOrEmpty(Context.SelectedGraphGuid))
            return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid);
            var ok = await GraphApi.CreateNodeAsync(graphGuid, _newNodeName.Trim());
            if (ok)
            {
                _newNodeName = "";
                await LoadNodesAsync();
            }
            else
            {
                _errorMessage = "创建节点失败";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "创建节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task DeleteNodeAsync(Guid nodeGuid)
    {
        if (!await ConfirmAsync("确定删除该节点及其关联边？")) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid);
            await GraphApi.DeleteNodeAsync(graphGuid, nodeGuid);
            _selectedNodeGuids.Remove(nodeGuid);
            await LoadNodesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "删除节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task DeleteSelectedNodesAsync()
    {
        if (_selectedNodeGuids.Count == 0) return;
        if (!await ConfirmAsync($"确定删除选中的 {_selectedNodeGuids.Count} 个节点？")) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid);
            foreach (var guid in _selectedNodeGuids)
                await GraphApi.DeleteNodeAsync(graphGuid, guid);

            _selectedNodeGuids.Clear();
            await LoadNodesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "批量删除节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task SaveNodeNameAsync(NodeDto node)
    {
        if (_editingNodeGuid != node.Guid) return;

        var newName = _editingNodeName.Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName == node.Name)
        {
            CancelEditNode();
            return;
        }

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid);
            var ok = await GraphApi.UpdateNodeAsync(graphGuid, node.Guid, newName);
            if (ok)
            {
                Logger.LogInformation("节点已重命名：{Guid} → {Name}", node.Guid, newName);
                CancelEditNode();
                await LoadNodesAsync();
            }
            else
            {
                _errorMessage = "重命名失败";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "重命名节点失败");
            _errorMessage = ex.Message;
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    // ─── UI 辅助 ─────────────────────────────────────
    private void ToggleNodeSelection(Guid guid, bool selected)
    {
        if (selected) _selectedNodeGuids.Add(guid);
        else _selectedNodeGuids.Remove(guid);
    }

    private void ShowNodeJson(NodeDto node) =>
        _detailJson = JsonSerializer.Serialize(node, PrettyJson);

    private void CloseDetailModal()
    {
        _detailJson = null;
    }

    private void StartEditNode(NodeDto node)
    {
        _editingNodeGuid = node.Guid;
        _editingNodeName = node.Name;
    }

    private void CancelEditNode()
    {
        _editingNodeGuid = null;
        _editingNodeName = "";
    }

    private async Task HandleEditKeyDown(KeyboardEventArgs e, Func<Task> onSave)
    {
        if (e.Key == "Enter") await onSave();
        else if (e.Key == "Escape") CancelEditNode();
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
