using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor;
using TreeGraph.TreeSky;
using TreeGraph.TreeSky.Components.Base;
using TreeGraph.TreeSky.Components.Nodes;
using TreeGraph.TreeSky.Models;
using Xunit;

namespace TreeGraph.TreeSky.Tests.Services;

/// <summary>
/// TreeNodeDialogService 单元测试。
/// Mock IDialogService，验证对话框参数传递与结果提取（含取消语义）。
///
/// 注意：
/// - MudBlazor 9.11 的 DialogResult 无公共构造函数，取消时 Cancel() 实际返回 null。
/// - ShowExAsync 是扩展方法，经由非泛型 IDialogService.ShowAsync(Type,...) 落地，故 mock 该重载。
/// </summary>
public class TreeNodeDialogServiceTests
{
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly BlazorService _blazorService = new();
    private readonly TreeNodeDialogService<StringTreeNode> _sut;

    public TreeNodeDialogServiceTests()
    {
        _sut = new TreeNodeDialogService<StringTreeNode>(
            _dialogService.Object, _blazorService);
    }

    // ============================================================
    // 辅助
    // ============================================================

    private void SetupDialogResult<TDialog>(DialogResult? result)
        where TDialog : IComponent
    {
        var dialogRef = new Mock<IDialogReference>();
        dialogRef.SetupGet(r => r.Result).Returns(Task.FromResult(result)!);

        _dialogService
            .Setup(s => s.ShowAsync<TDialog>(
                It.IsAny<string>(),
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRef.Object);
    }

    /// <summary>ShowExAsync 扩展方法最终走非泛型 ShowAsync(Type,...)，在此 mock。</summary>
    private void SetupExDialogResult<TDialog>(DialogResult? result)
        where TDialog : IComponent
    {
        var dialogRef = new Mock<IDialogReference>();
        dialogRef.SetupGet(r => r.Result).Returns(Task.FromResult(result)!);

        _dialogService
            .Setup(s => s.ShowAsync(
                typeof(TDialog),
                It.IsAny<string>(),
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRef.Object);
    }

    private static StringTreeNode MakeNode(string id, string? parentId = null) =>
        new() { Id = id, Name = $"Node-{id}", ParentId = parentId };

    private static NodeTemplate<StringTreeNode> MakeTemplate(string id) =>
        new() { Node = new TreeItemData<StringTreeNode> { Value = MakeNode(id) } };

    // ============================================================
    // ShowActionsDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowActionsDialogAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeActionsDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowActionsDialogAsync(MakeTemplate("1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowActionsDialogAsync_ReturnsActionResult()
    {
        var expected = new NodeActionResult<StringTreeNode>
        {
            Action = NodeAction.Edit,
            Node = MakeNode("1"),
        };
        SetupDialogResult<TreeNodeActionsDialog<StringTreeNode>>(DialogResult.Ok(expected));

        var result = await _sut.ShowActionsDialogAsync(MakeTemplate("1"));

        Assert.NotNull(result);
        Assert.Equal(NodeAction.Edit, result!.Action);
        Assert.Equal("1", result.Node.Id);
    }

    // ============================================================
    // ShowCreateDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowCreateDialogAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeEditDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowCreateDialogAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowCreateDialogAsync_ReturnsNode()
    {
        SetupDialogResult<TreeNodeEditDialog<StringTreeNode>>(
            DialogResult.Ok(MakeNode("new-1")));

        var result = await _sut.ShowCreateDialogAsync();

        Assert.NotNull(result);
        Assert.Equal("new-1", result!.Id);
    }

    // ============================================================
    // ShowEditDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowEditDialogAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeEditDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowEditDialogAsync(MakeTemplate("1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowEditDialogAsync_ReturnsNode()
    {
        SetupDialogResult<TreeNodeEditDialog<StringTreeNode>>(
            DialogResult.Ok(MakeNode("1")));

        var result = await _sut.ShowEditDialogAsync(MakeTemplate("1"));

        Assert.NotNull(result);
        Assert.Equal("1", result!.Id);
    }

    // ============================================================
    // ShowViewDialogAsync（返回 bool；取消时 Result 为 null）
    // ============================================================

    [Fact]
    public async Task ShowViewDialogAsync_Ok_ReturnsTrue()
    {
        SetupDialogResult<TreeNodeViewDialog<StringTreeNode>>(DialogResult.Ok(true));

        var result = await _sut.ShowViewDialogAsync(MakeNode("1"));

        Assert.True(result);
    }

    [Fact]
    public async Task ShowViewDialogAsync_Canceled_ReturnsFalse()
    {
        SetupDialogResult<TreeNodeViewDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowViewDialogAsync(MakeNode("1"));

        Assert.False(result);
    }

    // ============================================================
    // ShowSortDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowSortDialogAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeSortDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowSortDialogAsync(
            new TreeItemData<StringTreeNode> { Value = MakeNode("1") });

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowSortDialogAsync_ReturnsSortedList()
    {
        var sorted = new List<StringTreeNode> { MakeNode("2"), MakeNode("1") };
        SetupDialogResult<TreeNodeSortDialog<StringTreeNode>>(DialogResult.Ok(sorted));

        var result = await _sut.ShowSortDialogAsync(
            new TreeItemData<StringTreeNode> { Value = MakeNode("1") });

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("2", result[0].Id);
    }

    // ============================================================
    // ShowParentSelectDialogAsync（List 版本，泛型 ShowAsync）
    // ============================================================

    [Fact]
    public async Task ShowParentSelectDialogAsync_ListVersion_ReturnsResult()
    {
        var expected = new ParentSelectResult<StringTreeNode>
        {
            IsConfirmed = true,
            SelectedParent = MakeNode("parent-2"),
            SelectedPath = ["parent-2"],
        };
        SetupDialogResult<TreeNodeParentSelectDialog<StringTreeNode>>(DialogResult.Ok(expected));

        var result = await _sut.ShowParentSelectDialogAsync(
            [MakeNode("1")], currentNode: MakeNode("1"));

        Assert.NotNull(result);
        Assert.True(result!.IsConfirmed);
        Assert.Equal("parent-2", result.SelectedParent?.Id);
    }

    [Fact]
    public async Task ShowParentSelectDialogAsync_ListVersion_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeParentSelectDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ShowParentSelectDialogAsync([MakeNode("1")]);

        Assert.Null(result);
    }

    // ============================================================
    // ShowExAsync 路径（TreeSelectDialogSky / TreeDialogPageSky）
    // ============================================================

    [Fact]
    public async Task ShowDialogPageAsync_Ok_ReturnsNode()
    {
        SetupExDialogResult<TreeDialogPageSky<StringTreeNode>>(
            DialogResult.Ok(MakeNode("reg-1")));

        var result = await _sut.ShowDialogPageAsync();

        Assert.Equal("reg-1", result?.Id);
    }

    [Fact]
    public async Task ShowTreeSelectDialogAsync_Ok_ReturnsNode()
    {
        SetupExDialogResult<TreeSelectDialogSky<StringTreeNode>>(
            DialogResult.Ok(MakeNode("pick-1")));

        var result = await _sut.ShowTreeSelectDialogAsync();

        Assert.Equal("pick-1", result?.Id);
    }

    [Fact]
    public async Task ShowParentSelectDialogAsync_SingleNode_SameParent_ReturnsNull()
    {
        var currentParent = MakeNode("parent-1");
        var node = new TreeItemData<StringTreeNode>
        {
            Value = new StringTreeNode
            {
                Id = "child-1",
                Name = "Child",
                ParentId = "parent-1",
                Parent = currentParent,
            }
        };
        // 选中的节点 Id 与当前 ParentId 相同 → 无变化
        SetupExDialogResult<TreeSelectDialogSky<StringTreeNode>>(
            DialogResult.Ok(MakeNode("parent-1")));

        var result = await _sut.ShowParentSelectDialogAsync(node);

        Assert.Null(result);
    }

    // ============================================================
    // ExecuteNodeOperationAsync（便捷方法）
    // ============================================================

    [Fact]
    public async Task ExecuteNodeOperationAsync_Canceled_ReturnsNull()
    {
        SetupDialogResult<TreeNodeActionsDialog<StringTreeNode>>(DialogResult.Cancel());

        var result = await _sut.ExecuteNodeOperationAsync(MakeTemplate("1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteNodeOperationAsync_ReturnsOutcome()
    {
        var actionResult = new NodeActionResult<StringTreeNode>
        {
            Action = NodeAction.Delete,
            Node = MakeNode("1"),
        };
        SetupDialogResult<TreeNodeActionsDialog<StringTreeNode>>(DialogResult.Ok(actionResult));

        var result = await _sut.ExecuteNodeOperationAsync(MakeTemplate("1"));

        Assert.NotNull(result);
        Assert.Equal(NodeAction.Delete, result!.Action);
        Assert.Equal("1", result.Node.Id);
    }
}
