using MudBlazor;
using TreeGraph.Blazor.Shared.NodeEavSky.Components.Search;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Blazor.Shared.NodeEavSky.Services;

/// <summary>
/// 语义搜索节点选择器：打开 <see cref="NodePickerDialog"/>，返回用户选中的节点。
/// </summary>
public static class NodePicker
{
    /// <summary>
    /// 弹出节点选择对话框（标准 MudDialog 外观，不使用 MudBlazor.Extensions 外壳）。
    /// </summary>
    /// <param name="dialogService">MudBlazor 对话框服务。</param>
    /// <param name="options">弹窗尺寸等选项；为空使用默认（Medium 宽、铺满、右上角关闭）。</param>
    /// <param name="initialQuery">初始搜索词；非空时打开即搜索。</param>
    /// <param name="topK">返回数量（默认 20，上限 50）。</param>
    /// <param name="minScore">最低相似度（默认 0）。</param>
    /// <param name="title">对话框标题。</param>
    /// <param name="nodeType">限定宿主类型；为空不限。</param>
    /// <param name="excludeTreeNode">排除未挂载树宿主的裸节点（HostType=TreeNode）。</param>
    /// <returns>选中的搜索结果；取消时返回 <c>null</c>。</returns>
    public static async Task<SemanticSearchItemDto?> PickAsync(
        IDialogService dialogService,
        DialogOptions? options = null,
        string? initialQuery = null,
        int topK = 20,
        float minScore = 0f,
        string title = "选择节点",
        string? nodeType = null,
        bool excludeTreeNode = false)
    {
        var parameters = new DialogParameters<NodePickerDialog>
        {
            { x => x.Title, title },
            { x => x.InitialQuery, initialQuery },
            { x => x.TopK, topK },
            { x => x.MinScore, minScore },
            { x => x.NodeType, nodeType },
            { x => x.ExcludeTreeNode, excludeTreeNode },
        };

        options ??= new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true,
            Position = DialogPosition.TopCenter,
        };

        var dialog = await dialogService.ShowAsync<NodePickerDialog>(title, parameters, options);
        var result = await dialog.Result;

        return result is { Canceled: false, Data: SemanticSearchItemDto item } ? item : null;
    }
}
