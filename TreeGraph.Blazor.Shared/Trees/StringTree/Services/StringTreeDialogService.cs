using Microsoft.AspNetCore.Components;
using MudBlazor;
using MudBlazor.Extensions;
using TreeGraph.Blazor.Shared.Trees.TreeSky.Dialogs;
using TreeGraph.Blazor.Shared.Trees.StringTree.Components;
using TreeGraph.Blazor.Shared.Trees.StringTree.Contracts;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;

namespace TreeGraph.Blazor.Shared.Trees.StringTree.Services;

/// <summary>
/// StringTreeSky 对话框服务。
///
/// 对话框选项统一走 <see cref="DialogConfig"/> 的 ToDialogOptions()；
/// 所有对话框返回弱类型 result，由本服务做类型校验后向上传递。
/// </summary>
public class StringTreeDialogService(
    IDialogService dialogService)
{
    // ============================================================
    // 操作选择
    // ============================================================

    public async Task<StringNodeActionResult?> ShowActionsDialogAsync(
        StringNodeMeta node,
        StringNodeMeta? parentNode,
        bool isBoot = false,
        bool hasChildren = false,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "Node", node },
            { "ParentNode", parentNode },
            { "IsBoot", isBoot },
            { "HasChildren", hasChildren },
        };

        var options = (config ?? new DialogConfig { MaxWidth = MaxWidth.Small })
            .ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringNodeActionsDialog>(
            "节点操作", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not StringNodeActionResult ar)
            return null;

        return ar;
    }

    // ============================================================
    // 查看详情
    // ============================================================

    public async Task<bool> ShowViewDialogAsync(
        StringNodeMeta node,
        StringNodeMeta? parentNode = null,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "Node", node },
            { "ParentNode", parentNode },
        };

        var options = (config ?? new DialogConfig { MaxWidth = MaxWidth.Small })
            .ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringNodeViewDialog>(
            "节点详情", parameters, options);

        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    // ============================================================
    // 创建 / 编辑
    // ============================================================

    /// <summary>创建子节点：入参父节点；返回新建的元数据（ParentId 已填）。</summary>
    public async Task<StringNodeMeta?> ShowCreateDialogAsync(
        StringNodeMeta parent,
        RenderFragment<StringNodeMeta>? editTemplate = null,
        DialogConfig? config = null)
    {
        var template = new StringNodeTemplate
        {
            Node = new StringNodeMeta
            {
                ParentId = parent.Id,
                CanHaveChildren = true,
            },
            EditTemplate = editTemplate,
        };

        return await ShowEditDialogAsync(template, parent, isCreate: true, config);
    }

    /// <summary>编辑节点：入参目标节点；返回修改后的元数据。</summary>
    public async Task<StringNodeMeta?> ShowEditDialogAsync(
        StringNodeMeta node,
        RenderFragment<StringNodeMeta>? editTemplate = null,
        DialogConfig? config = null)
    {
        var template = new StringNodeTemplate
        {
            Node = node,
            EditTemplate = editTemplate,
        };

        return await ShowEditDialogAsync(template, parentNode: null, isCreate: false, config);
    }

    private async Task<StringNodeMeta?> ShowEditDialogAsync(
        StringNodeTemplate template,
        StringNodeMeta? parentNode,
        bool isCreate,
        DialogConfig? config)
    {
        var parameters = new DialogParameters
        {
            { "Template", template },
            { "ParentNode", parentNode },
            { "IsCreate", isCreate },
        };

        var options = (config ?? new DialogConfig { MaxWidth = MaxWidth.Small })
            .ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringNodeEditDialog>(
            isCreate ? "创建节点" : "编辑节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not StringNodeMeta meta)
            return null;

        return meta;
    }

    // ============================================================
    // 排序
    // ============================================================

    /// <summary>
    /// 排序对话框。入参为有序子节点列表，返回重排后的 ID 列表（顺序即新顺序）。
    /// </summary>
    public async Task<List<string>?> ShowSortDialogAsync(
        IReadOnlyList<StringNodeMeta> children,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "Children", children },
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.Small,
            CloseButton = false,
            BackdropClick = false,
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringNodeSortDialog>(
            "拖拽排序", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not List<string> sortResult)
            return null;

        return sortResult;
    }

    // ============================================================
    // 父节点选择
    // ============================================================

    /// <summary>
    /// 从扁平节点列表中选择父节点。
    /// 组件内部按 ParentId 构建树形视图，无需 Children 属性。
    /// </summary>
    public async Task<StringParentSelectResult?> ShowParentSelectDialogAsync(
        IReadOnlyList<StringNodeMeta> allNodes,
        StringNodeMeta? currentNode = null,
        StringNodeMeta? currentParent = null,
        bool allowRoot = true,
        DialogConfig? config = null)
    {
        var parameters = new DialogParameters
        {
            { "AllNodes", allNodes },
            { "CurrentNode", currentNode },
            { "CurrentParent", currentParent },
            { "AllowRootSelection", allowRoot },
        };

        var options = (config ?? new DialogConfig
        {
            MaxWidth = MaxWidth.Medium,
            CloseButton = true,
        }).ToDialogOptions();

        var dialog = await dialogService.ShowAsync<StringParentSelectDialog>(
            "选择父节点", parameters, options);

        var result = await dialog.Result;
        if (result?.Canceled != false || result.Data is not StringParentSelectResult sr)
            return null;

        return sr;
    }
}
