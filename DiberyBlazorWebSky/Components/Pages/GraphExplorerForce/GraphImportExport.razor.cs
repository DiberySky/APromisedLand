using DiberyBlazorWebSky.Models.Graph;
using DiberyBlazorWebSky.Services;
using DiberyBlazorWebSky.Components.Pages.GraphExplorerForce.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Forms;

namespace DiberyBlazorWebSky.Components.Pages.GraphExplorerForce;

public partial class GraphImportExport : ComponentBase, IDisposable
{
    [Inject] private IGraphPageContext Context { get; set; } = default!;
    [Inject] private GraphAdminApiClient GraphApi { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<GraphImportExport> Logger { get; set; } = default!;

    private bool _isBusy;
    private string? _errorMessage;
    private string _progress = "";

    // 导入 / 导出状态
    private string? _selectedJsonFileName;
    private string? _selectedJsonContent;
    private string? _selectedNodesCsvName;
    private string? _selectedNodesCsvContent;
    private string? _selectedEdgesCsvName;
    private string? _selectedEdgesCsvContent;
    private bool _clearBeforeImport;
    private ImportResult? _importResult;

    private bool HasAnyImportFile =>
        !string.IsNullOrEmpty(_selectedJsonContent) ||
        !string.IsNullOrEmpty(_selectedNodesCsvContent);

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
        ResetImportState();
        StateHasChanged();
        return Task.CompletedTask;
    }

    // ══════════════════════════════════════════════════════
    // 导出
    // ══════════════════════════════════════════════════════

    private async Task ExportJsonAsync()
    {
        if (string.IsNullOrEmpty(Context.SelectedGraphGuid)) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);
            var graphName = Context.Graphs.FirstOrDefault(g => g.Guid == graphGuid)?.Name ?? "graph";
            var json = await GraphApi.ExportGraphJsonAsync(graphGuid);

            if (string.IsNullOrEmpty(json)) { _errorMessage = "导出失败"; return; }

            var safeName = SanitizeFileName(graphName);
            var fileName = $"{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            await Js.InvokeVoidAsync("downloadTextFile", fileName, json, "application/json");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导出 JSON 失败");
            _errorMessage = $"导出失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task ExportNodesCsvAsync()
    {
        if (string.IsNullOrEmpty(Context.SelectedGraphGuid)) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);
            var graphName = Context.Graphs.FirstOrDefault(g => g.Guid == graphGuid)?.Name ?? "graph";
            var csv = await GraphApi.ExportNodesCsvAsync(graphGuid);

            if (string.IsNullOrEmpty(csv)) { _errorMessage = "导出失败"; return; }

            var safeName = SanitizeFileName(graphName);
            var fileName = $"{safeName}-nodes-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            await Js.InvokeVoidAsync("downloadTextFile", fileName, csv, "text/csv;charset=utf-8");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导出节点 CSV 失败");
            _errorMessage = $"导出失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    private async Task ExportEdgesCsvAsync()
    {
        if (string.IsNullOrEmpty(Context.SelectedGraphGuid)) return;

        _isBusy = true;
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);
            var graphName = Context.Graphs.FirstOrDefault(g => g.Guid == graphGuid)?.Name ?? "graph";
            var csv = await GraphApi.ExportEdgesCsvAsync(graphGuid);

            if (string.IsNullOrEmpty(csv)) { _errorMessage = "导出失败"; return; }

            var safeName = SanitizeFileName(graphName);
            var fileName = $"{safeName}-edges-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            await Js.InvokeVoidAsync("downloadTextFile", fileName, csv, "text/csv;charset=utf-8");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导出边 CSV 失败");
            _errorMessage = $"导出失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            StateHasChanged();
        }
    }

    // ══════════════════════════════════════════════════════
    // 导入
    // ══════════════════════════════════════════════════════

    private async Task StartImportAsync()
    {
        if (string.IsNullOrEmpty(Context.SelectedGraphGuid)) return;
        if (!HasAnyImportFile) return;

        var clearWarning = _clearBeforeImport
            ? "⚠️ 将先清空图中现有节点和边！此操作不可撤销。\n\n"
            : "";
        if (!await ConfirmAsync($"{clearWarning}确定导入？"))
            return;

        _isBusy = true;
        _errorMessage = null;
        _importResult = null;
        _progress = "解析文件中...";
        StateHasChanged();

        try
        {
            var graphGuid = Guid.Parse(Context.SelectedGraphGuid!);

            ImportResultDto dto;
            if (!string.IsNullOrEmpty(_selectedJsonContent))
            {
                dto = await GraphApi.ImportJsonAsync(graphGuid, _selectedJsonContent, _clearBeforeImport);
            }
            else
            {
                dto = await GraphApi.ImportCsvAsync(
                    graphGuid, _selectedNodesCsvContent!, _selectedEdgesCsvContent, _clearBeforeImport);
            }

            _importResult = new ImportResult
            {
                NodesCreated = dto.NodesCreated,
                NodesSkipped = dto.NodesSkipped,
                EdgesCreated = dto.EdgesCreated,
                EdgesSkipped = dto.EdgesSkipped,
                Errors = dto.Errors
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "导入失败");
            _errorMessage = $"导入失败：{ex.Message}";
        }
        finally
        {
            _isBusy = false;
            _progress = "";
            StateHasChanged();
        }
    }

    private async Task OnJsonFileSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var reader = new StreamReader(stream);
            _selectedJsonContent = await reader.ReadToEndAsync();
            _selectedJsonFileName = $"{file.Name} ({FormatFileSize(file.Size)})";
            _importResult = null;

            _selectedNodesCsvContent = null;
            _selectedNodesCsvName = null;
            _selectedEdgesCsvContent = null;
            _selectedEdgesCsvName = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读取 JSON 文件失败");
            _errorMessage = $"读取文件失败：{ex.Message}";
            _selectedJsonFileName = null;
            _selectedJsonContent = null;
        }

        StateHasChanged();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024):F1} MB";
    }

    private async Task OnNodesCsvSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var reader = new StreamReader(stream);
            _selectedNodesCsvContent = await reader.ReadToEndAsync();
            _selectedNodesCsvName = $"{file.Name} ({file.Size / 1024} KB)";
            _importResult = null;

            _selectedJsonContent = null;
            _selectedJsonFileName = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读取节点 CSV 失败");
            _errorMessage = $"读取文件失败：{ex.Message}";
            _selectedNodesCsvName = null;
            _selectedNodesCsvContent = null;
        }

        StateHasChanged();
    }

    private async Task OnEdgesCsvSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
            using var reader = new StreamReader(stream);
            _selectedEdgesCsvContent = await reader.ReadToEndAsync();
            _selectedEdgesCsvName = $"{file.Name} ({file.Size / 1024} KB)";
            _importResult = null;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读取边 CSV 失败");
            _errorMessage = $"读取文件失败：{ex.Message}";
            _selectedEdgesCsvName = null;
            _selectedEdgesCsvContent = null;
        }

        StateHasChanged();
    }

    private void ResetImportState()
    {
        _selectedJsonFileName = null;
        _selectedJsonContent = null;
        _selectedNodesCsvName = null;
        _selectedNodesCsvContent = null;
        _selectedEdgesCsvName = null;
        _selectedEdgesCsvContent = null;
        _importResult = null;
        _clearBeforeImport = false;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString();
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
