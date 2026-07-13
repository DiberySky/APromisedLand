using APromisedLand.Shared.DiberyTree.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace APromisedLand.Razor.DiberyTree;

public partial class TreePage : ComponentBase
{private List<TreeViewItemData<TreeNodeDto<string>>> _rootItems = new();
    private TreeNodeDto<string>? _selectedNode;

    protected override async Task OnInitializedAsync()
    {
        await LoadRootNodes();
    }

    /// <summary>
    /// 加载根节点并构建树
    /// </summary>
    private async Task LoadRootNodes()
    {
        var roots = await TreeService.GetRootNodesAsync();
        _rootItems = roots.Select(root => BuildTreeItem(root)).ToList();
        StateHasChanged();
    }

    /// <summary>
    /// 递归构建 TreeViewItemData，每个节点使用 getChildren 实现懒加载
    /// </summary>
    private TreeViewItemData<TreeNodeDto<string>> BuildTreeItem(TreeNodeDto<string> node)
    {
        return new TreeViewItemData<TreeNodeDto<string>>(
            value: node,
            getChildren: async () =>
            {
                var children = await TreeService.GetChildrenAsync(node.Id);
                return children.Select(child => BuildTreeItem(child)).ToList();
            }
        );
    }

    /// <summary>
    /// 添加根节点（弹出对话框输入名称）
    /// </summary>
    private async Task AddRootNode()
    {
        var result = DialogService.ShowAsync<AddNodeDialog>( "添加根节点", new DialogParameters { ["Title"] = "新建根节点" }).Result;
        if (!result.Cancelled && result.Data is string text && !string.IsNullOrWhiteSpace(text))
        {
            var newNode = new TreeNodeDto<string>
            {
                Id = Guid.NewGuid().ToString(), // 临时，服务端会重新生成
                Text = text,
                ParentId = null,
                HasChildren = false,
                Icon = "folder"
            };
            await TreeService.CreateNodeAsync(newNode);
            await LoadRootNodes();
            Snackbar.Add($"根节点“{text}”已添加", Severity.Success);
        }
    }

    /// <summary>
    /// 添加子节点（在节点模板中调用）
    /// </summary>
    private async Task AddChildNode(TreeNodeDto<string> parent)
    {
        var result = DialogService.ShowAsync<AddNodeDialog>("添加子节点", new DialogParameters { ["Title"] = $"添加子节点到“{parent.Text}”" }).Result;
        if (!result.CanCancel && result.Data is string text && !string.IsNullOrWhiteSpace(text))
        {
            var newNode = new TreeNodeDto<string>
            {
                Id = Guid.NewGuid().ToString(),
                Text = text,
                ParentId = parent.Id,
                HasChildren = false,
                Icon = "insert_drive_file"
            };
            await TreeService.CreateNodeAsync(newNode);
            await LoadRootNodes(); // 刷新整棵树（展开状态会丢失，可优化）
            Snackbar.Add($"子节点“{text}”已添加", Severity.Success);
        }
    }

    /// <summary>
    /// 删除节点（包含确认对话框）
    /// </summary>
    private async Task DeleteNode(TreeNodeDto<string> node)
    {
        bool? confirm = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定要删除节点“{node.Text}”及其所有子节点吗？",
            "删除",
            "取消");
        if (confirm == true)
        {
            await TreeService.DeleteNodeAsync(node.Id);
            await LoadRootNodes();
            Snackbar.Add($"节点“{node.Text}”已删除", Severity.Success);
        }
    }

    /// <summary>
    /// 自定义节点模板，显示图标、文本、操作按钮
    /// </summary>
    private RenderFragment<TreeViewItemData<TreeNodeDto<string>>> ItemTemplate => (item) => builder =>
    {
        var node = item.Value;
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "style", "display: flex; align-items: center; width: 100%; gap: 8px;");

        // 图标 + 文本
        builder.OpenElement(2, "span");
        builder.AddAttribute(3, "style", "flex: 1;");
        if (!string.IsNullOrEmpty(node.Icon))
        {
            builder.OpenComponent<MudIcon>(4);
            builder.AddAttribute(5, "Icon", node.Icon);
            builder.AddAttribute(6, "Size", Size.Small);
            builder.AddAttribute(7, "Class", "mr-2");
            builder.CloseComponent();
        }
        builder.AddContent(8, node.Text);
        builder.CloseElement(); // span

        // 操作按钮组
        builder.OpenElement(9, "div");
        builder.AddAttribute(10, "style", "display: flex; gap: 4px;");

        // 添加子节点
        builder.OpenComponent<MudIconButton>(11);
        builder.AddAttribute(12, "Icon", Icons.Material.Filled.Add);
        builder.AddAttribute(13, "Size", Size.Small);
        builder.AddAttribute(14, "Color", Color.Success);
        builder.AddAttribute(15, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, () => AddChildNode(node)));
        builder.CloseComponent();

        // 删除
        builder.OpenComponent<MudIconButton>(16);
        builder.AddAttribute(17, "Icon", Icons.Material.Filled.Delete);
        builder.AddAttribute(18, "Size", Size.Small);
        builder.AddAttribute(19, "Color", Color.Error);
        builder.AddAttribute(20, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, () => DeleteNode(node)));
        builder.CloseComponent();

        builder.CloseElement(); // 按钮组
        builder.CloseElement(); // 外层 div
    };
}