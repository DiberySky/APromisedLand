using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Web;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;

public partial class GraphSelectorBar : ComponentBase, IDisposable
{
    [Inject] private IGraphPageContext Context { get; set; } = default!;

    private bool _showManageDialog;

    protected override void OnInitialized()
    {
        Context.StateChanged += OnContextStateChanged;
    }

    private async Task OnGraphChanged(ChangeEventArgs e)
    {
        var guid = e.Value?.ToString();
        await Context.SelectGraphAsync(guid);
    }

    private async Task RefreshAsync()
    {
        await Context.LoadGraphsAsync();
    }

    private void OpenManageDialog()
    {
        _showManageDialog = true;
    }

    private void CloseManageDialog()
    {
        _showManageDialog = false;
    }

    private Task OnContextStateChanged()
    {
        StateHasChanged();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Context.StateChanged -= OnContextStateChanged;
    }
}
