using DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;
using Microsoft.AspNetCore.Components;

namespace DiberyBlazorWebSky.Components.Pages;

public partial class McpGraphExplorerForce : ComponentBase, IDisposable
{
    [Inject] private IGraphPageContext Context { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        Context.StateChanged += OnContextStateChanged;
        if (Context.Graphs.Count == 0)
        {
            await Context.LoadGraphsAsync();
        }
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
