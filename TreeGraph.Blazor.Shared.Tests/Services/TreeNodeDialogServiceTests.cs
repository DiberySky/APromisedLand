using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor;
using TreeGraph.Blazor.Shared.TreeEavSky;
using TreeGraph.Blazor.Shared.TreeEavSky.Contracts;
using TreeGraph.Blazor.Shared.TreeEavSky.Dialogs;
using TreeGraph.Blazor.Shared.TreeEavSky.Nodes;
using TreeGraph.Shared.TreeEavSky.Entities;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Services;

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
    private readonly BlazorService _blazorService;
    private readonly TreeNodeDialogService<UnitTree> _sut;

    public TreeNodeDialogServiceTests()
    {
        _blazorService = new BlazorService(_dialogService.Object);
        _sut = new TreeNodeDialogService<UnitTree>(
            _dialogService.Object, _blazorService);
    }

    // ============================================================
    // 辅助
    // ============================================================

    /// <summary>ShowExAsync 扩展方法最终走非泛型 ShowAsync(Type,...)，在此 mock。</summary>
    private void SetupExDialogResult<TDialog>(DialogResult? result)
        where TDialog : IComponent
    {
        var dialogRef = new Mock<IDialogReference>();
        dialogRef.SetupGet(r => r.Result).Returns(Task.FromResult(result));

        _dialogService
            .Setup(s => s.ShowAsync(
                typeof(TDialog),
                It.IsAny<string>(),
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRef.Object);
    }

    private static UnitTree MakeNode(string id, string? parentId = null) =>
        new() { Id = id, Name = $"Node-{id}", ParentId = parentId };

    private static NodeTemplate<UnitTree> MakeTemplate(string id) =>
        new() { Node = new TreeItemData<UnitTree> { Value = MakeNode(id) } };

    // ============================================================
    // ShowActionsDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowActionsDialogAsync_Canceled_ReturnsNull()
    {
        SetupExDialogResult<TreeNodeActionsDialog<UnitTree>>(DialogResult.Cancel());

        var result = await _sut.ShowActionsDialogAsync(MakeTemplate("1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowActionsDialogAsync_ReturnsActionResult()
    {
        var expected = new NodeActionResult<UnitTree>
        {
            Action = NodeAction.Edit,
            Node = MakeNode("1"),
        };
        SetupExDialogResult<TreeNodeActionsDialog<UnitTree>>(DialogResult.Ok(expected));

        var result = await _sut.ShowActionsDialogAsync(MakeTemplate("1"));

        Assert.NotNull(result);
        Assert.Equal(NodeAction.Edit, result.Action);
        Assert.Equal("1", result.Node.Id);
    }

    // ============================================================
    // ShowCreateDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowCreateDialogAsync_Canceled_ReturnsNull()
    {
        SetupExDialogResult<TreeNodeEditDialog<UnitTree>>(DialogResult.Cancel());

        var result = await _sut.ShowCreateDialogAsync();

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowCreateDialogAsync_ReturnsNode()
    {
        SetupExDialogResult<TreeNodeEditDialog<UnitTree>>(
            DialogResult.Ok(MakeNode("new-1")));

        var result = await _sut.ShowCreateDialogAsync();

        Assert.NotNull(result);
        Assert.Equal("new-1", result.Id);
    }

    // ============================================================
    // ShowEditDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowEditDialogAsync_Canceled_ReturnsNull()
    {
        SetupExDialogResult<TreeNodeEditDialog<UnitTree>>(DialogResult.Cancel());

        var result = await _sut.ShowEditDialogAsync(MakeTemplate("1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowEditDialogAsync_ReturnsNode()
    {
        SetupExDialogResult<TreeNodeEditDialog<UnitTree>>(
            DialogResult.Ok(MakeNode("1")));

        var result = await _sut.ShowEditDialogAsync(MakeTemplate("1"));

        Assert.NotNull(result);
        Assert.Equal("1", result.Id);
    }

    // ============================================================
    // ShowViewDialogAsync（返回 bool；取消时 Result 为 null）
    // ============================================================

    [Fact]
    public async Task ShowViewDialogAsync_Ok_ReturnsTrue()
    {
        SetupExDialogResult<TreeNodeViewDialog<UnitTree>>(DialogResult.Ok(true));

        var result = await _sut.ShowViewDialogAsync(MakeNode("1"));

        Assert.True(result);
    }

    [Fact]
    public async Task ShowViewDialogAsync_Canceled_ReturnsFalse()
    {
        SetupExDialogResult<TreeNodeViewDialog<UnitTree>>(DialogResult.Cancel());

        var result = await _sut.ShowViewDialogAsync(MakeNode("1"));

        Assert.False(result);
    }

    // ============================================================
    // ShowSortDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowSortDialogAsync_Canceled_ReturnsNull()
    {
        SetupExDialogResult<TreeNodeSortDialog<UnitTree>>(DialogResult.Cancel());

        var result = await _sut.ShowSortDialogAsync(
            new TreeItemData<UnitTree> { Value = MakeNode("1") });

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowSortDialogAsync_ReturnsSortedList()
    {
        var sorted = new List<UnitTree> { MakeNode("2"), MakeNode("1") };
        SetupExDialogResult<TreeNodeSortDialog<UnitTree>>(DialogResult.Ok(sorted));

        var result = await _sut.ShowSortDialogAsync(
            new TreeItemData<UnitTree> { Value = MakeNode("1") });

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("2", result[0].Id);
    }

    // ============================================================
    // ShowParentSelectDialogAsync（List 版本）
    // ============================================================

    [Fact]
    public async Task ShowParentSelectDialogAsync_ListVersion_ReturnsResult()
    {
        var expected = new ParentSelectResult<UnitTree>
        {
            IsConfirmed = true,
            SelectedParent = MakeNode("parent-2"),
            SelectedPath = ["parent-2"],
        };
        SetupExDialogResult<TreeNodeParentSelectDialog<UnitTree>>(DialogResult.Ok(expected));

        var result = await _sut.ShowParentSelectDialogAsync(
            [MakeNode("1")], currentNode: MakeNode("1"));

        Assert.NotNull(result);
        Assert.True(result.IsConfirmed);
        Assert.Equal("parent-2", result.SelectedParent?.Id);
    }

    [Fact]
    public async Task ShowParentSelectDialogAsync_ListVersion_Canceled_ReturnsNull()
    {
        SetupExDialogResult<TreeNodeParentSelectDialog<UnitTree>>(DialogResult.Cancel());

        var result = await _sut.ShowParentSelectDialogAsync([MakeNode("1")]);

        Assert.Null(result);
    }

    // ============================================================
    // ShowParentSelectDialogAsync（单节点版本，含同父校验）
    // ============================================================

    [Fact]
    public async Task ShowDialogPageAsync_Ok_ReturnsNode()
    {
        SetupExDialogResult<TreeDialogPageSky<UnitTree>>(
            DialogResult.Ok(MakeNode("reg-1")));

        var result = await _sut.ShowDialogPageAsync();

        Assert.Equal("reg-1", result?.Id);
    }

    [Fact]
    public async Task ShowTreeSelectDialogAsync_Ok_ReturnsNode()
    {
        SetupExDialogResult<TreeSelectDialogSky<UnitTree>>(
            DialogResult.Ok(MakeNode("pick-1")));

        var result = await _sut.ShowTreeSelectDialogAsync();

        Assert.Equal("pick-1", result?.Id);
    }

    [Fact]
    public async Task ShowParentSelectDialogAsync_SingleNode_SameParent_ReturnsNull()
    {
        var currentParent = MakeNode("parent-1");
        var node = new TreeItemData<UnitTree>
        {
            Value = new UnitTree
            {
                Id = "child-1",
                Name = "Child",
                ParentId = "parent-1",
                Parent = currentParent,
            }
        };
        // 选中的节点 Id 与当前 ParentId 相同 → 无变化
        SetupExDialogResult<TreeSelectDialogSky<UnitTree>>(
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
        SetupExDialogResult<TreeNodeActionsDialog<UnitTree>>(DialogResult.Cancel());

        var result = await _sut.ExecuteNodeOperationAsync(MakeTemplate("1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteNodeOperationAsync_ReturnsOutcome()
    {
        var actionResult = new NodeActionResult<UnitTree>
        {
            Action = NodeAction.Delete,
            Node = MakeNode("1"),
        };
        SetupExDialogResult<TreeNodeActionsDialog<UnitTree>>(DialogResult.Ok(actionResult));

        var result = await _sut.ExecuteNodeOperationAsync(MakeTemplate("1"));

        Assert.NotNull(result);
        Assert.Equal(NodeAction.Delete, result.Action);
        Assert.Equal("1", result.Node.Id);
    }
}
