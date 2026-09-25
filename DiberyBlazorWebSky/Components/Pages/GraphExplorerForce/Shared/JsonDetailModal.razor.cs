using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;

public partial class JsonDetailModal : ComponentBase
{
    [Parameter] public string? Json { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private Task Close() => OnClose.InvokeAsync();
}
