using DiberyBlazorWebSky.Models.Graph;
using DiberyBlazorWebSky.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;

public partial class GraphManageDialog : ComponentBase
{
    [Inject] private IGraphPageContext Context { get; set; } = default!;
    [Inject] private GraphAdminApiClient GraphApi { get; set; } = default!;
    [Inject] private ILogger<GraphManageDialog> Logger { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private bool _isManaging;
    private string _newGraphName = "";
    private bool _isCreatingGraph;
    private Guid? _renamingGraphGuid;
    private string _renamingGraphName = "";
    private string? _errorMessage;

    private async Task CloseAsync()
    {
        _errorMessage = null;
        await OnClose.InvokeAsync();
    }

    private async Task CreateGraphAsync()
    {
        if (string.IsNullOrWhiteSpace(_newGraphName)) return;

        _isCreatingGraph = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var ok = await GraphApi.CreateGraphAsync(_newGraphName.Trim());
            if (ok)
            {
                _newGraphName = "";
                await Context.RefreshAsync();
            }
            else
            {
                _errorMessage = "创建图失败。";
            }
        }
        finally
        {
            _isCreatingGraph = false;
            StateHasChanged();
        }
    }

    private async Task ConfirmRenameAsync()
    {
        if (_renamingGraphGuid is null || string.IsNullOrWhiteSpace(_renamingGraphName))
            return;

        var newName = _renamingGraphName.Trim();
        var existing = Context.Graphs.FirstOrDefault(g => g.Guid == _renamingGraphGuid.Value);

        if (existing is null || existing.Name == newName)
        {
            CancelRename();
            return;
        }

        _isManaging = true;
        StateHasChanged();

        try
        {
            var ok = await GraphApi.UpdateGraphAsync(existing.Guid, newName);
            if (ok)
            {
                existing.Name = newName;
                CancelRename();
            }
            else
            {
                _errorMessage = "重命名失败。";
            }
        }
        finally
        {
            _isManaging = false;
            StateHasChanged();
        }
    }

    private async Task DeleteGraphAsync(GraphDto g)
    {
        var confirmed = await Js.InvokeAsync<bool>("confirm",
            $"删除图 '{g.Name}' 将同时删除其所有节点和边，此操作不可撤销。\n\n确定删除？");
        if (!confirmed) return;

        _isManaging = true;
        _errorMessage = null;
        StateHasChanged();

        try
        {
            var ok = await GraphApi.DeleteGraphAsync(g.Guid);
            if (ok)
            {
                if (Context.SelectedGraphGuid == g.Guid.ToString())
                {
                    await Context.SelectGraphAsync(null);
                }

                await Context.RefreshAsync();
            }
            else
            {
                _errorMessage = "删除失败。";
            }
        }
        finally
        {
            _isManaging = false;
            StateHasChanged();
        }
    }

    private void StartRename(GraphDto g)
    {
        _renamingGraphGuid = g.Guid;
        _renamingGraphName = g.Name;
        StateHasChanged();
    }

    private void CancelRename()
    {
        _renamingGraphGuid = null;
        _renamingGraphName = "";
        StateHasChanged();
    }
}
