using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor;
using TreeGraph.Blazor.Shared.Trees.StringTree.Components;
using TreeGraph.Blazor.Shared.Trees.StringTree.Contracts;
using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Services;

/// <summary>
/// StringTreeDialogService 单元测试。
/// 参考 TreeNodeDialogServiceTests 模式（Moq IDialogService + 类型化结果解析）。
///
/// 注意：MudBlazor 9.11 的 DialogResult 无公共构造函数，Cancel 用 DialogResult.Cancel()。
/// </summary>
public class StringTreeDialogServiceTests
{
    private readonly Mock<IDialogService> _dialog = new();
    private readonly StringTreeDialogService _sut;

    public StringTreeDialogServiceTests()
    {
        _sut = new StringTreeDialogService(_dialog.Object);
    }

    // ============================================================
    // 辅助
    // ============================================================

    private void SetupResult<TDialog>(DialogResult? result)
        where TDialog : IComponent
    {
        var dialogRef = new Mock<IDialogReference>();
        dialogRef.SetupGet(r => r.Result).Returns(Task.FromResult(result)!);

        _dialog
            .Setup(s => s.ShowAsync<TDialog>(
                It.IsAny<string>(),
                It.IsAny<DialogParameters>(),
                It.IsAny<DialogOptions>()))
            .ReturnsAsync(dialogRef.Object);
    }

    private static StringNodeMeta Meta(string id, string? parentId = null)
        => new() { Id = id, Text = $"节点-{id}", ParentId = parentId };

    // ============================================================
    // ShowActionsDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowActionsDialogAsync_Canceled_ReturnsNull()
    {
        SetupResult<StringNodeActionsDialog>(DialogResult.Cancel());

        var result = await _sut.ShowActionsDialogAsync(Meta("1"), parentNode: null);

        Assert.Null(result);
    }

    [Fact]
    public async Task ShowActionsDialogAsync_ReturnsActionResult()
    {
        var expected = new StringNodeActionResult
        {
            Action = StringNodeAction.Edit,
            Node = Meta("1"),
        };
        SetupResult<StringNodeActionsDialog>(DialogResult.Ok(expected));

        var result = await _sut.ShowActionsDialogAsync(Meta("1"), parentNode: null);

        Assert.NotNull(result);
        Assert.Equal(StringNodeAction.Edit, result!.Action);
        Assert.Equal("1", result.Node.Id);
    }

    [Fact]
    public async Task ShowActionsDialogAsync_TypeMismatch_ReturnsNull()
    {
        // 结果不是 StringNodeActionResult → 静默返回 null（防御性设计）
        SetupResult<StringNodeActionsDialog>(DialogResult.Ok("wrong-type"));

        var result = await _sut.ShowActionsDialogAsync(Meta("1"), parentNode: null);

        Assert.Null(result);
    }

    // ============================================================
    // ShowViewDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowViewDialogAsync_Ok_ReturnsTrue()
    {
        SetupResult<StringNodeViewDialog>(DialogResult.Ok(true));

        var result = await _sut.ShowViewDialogAsync(Meta("1"));

        Assert.True(result);
    }

    [Fact]
    public async Task ShowViewDialogAsync_Canceled_ReturnsFalse()
    {
        SetupResult<StringNodeViewDialog>(DialogResult.Cancel());

        var result = await _sut.ShowViewDialogAsync(Meta("1"));

        Assert.False(result);
    }

    // ============================================================
    // ShowCreateDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowCreateDialogAsync_ReturnsMetaWithParentId()
    {
        SetupResult<StringNodeEditDialog>(DialogResult.Ok(Meta("new-1", "p1")));

        var result = await _sut.ShowCreateDialogAsync(Meta("p1"));

        Assert.NotNull(result);
        Assert.Equal("new-1", result!.Id);
    }

    [Fact]
    public async Task ShowCreateDialogAsync_Canceled_ReturnsNull()
    {
        SetupResult<StringNodeEditDialog>(DialogResult.Cancel());

        var result = await _sut.ShowCreateDialogAsync(Meta("p1"));

        Assert.Null(result);
    }

    // ============================================================
    // ShowEditDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowEditDialogAsync_ReturnsMeta()
    {
        SetupResult<StringNodeEditDialog>(DialogResult.Ok(Meta("1")));

        var result = await _sut.ShowEditDialogAsync(Meta("1"));

        Assert.NotNull(result);
        Assert.Equal("1", result!.Id);
    }

    [Fact]
    public async Task ShowEditDialogAsync_Canceled_ReturnsNull()
    {
        SetupResult<StringNodeEditDialog>(DialogResult.Cancel());

        var result = await _sut.ShowEditDialogAsync(Meta("1"));

        Assert.Null(result);
    }

    // ============================================================
    // ShowSortDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowSortDialogAsync_ReturnsOrderedIdList()
    {
        var ordered = new List<string> { "2", "1", "3" };
        SetupResult<StringNodeSortDialog>(DialogResult.Ok(ordered));

        var result = await _sut.ShowSortDialogAsync(new[]
        {
            Meta("1"), Meta("2"), Meta("3")
        });

        Assert.NotNull(result);
        Assert.Equal(new[] { "2", "1", "3" }, result!);   // ★ 顺序即新顺序
    }

    [Fact]
    public async Task ShowSortDialogAsync_Canceled_ReturnsNull()
    {
        SetupResult<StringNodeSortDialog>(DialogResult.Cancel());

        var result = await _sut.ShowSortDialogAsync(new[] { Meta("1"), Meta("2") });

        Assert.Null(result);
    }

    // ============================================================
    // ShowParentSelectDialogAsync
    // ============================================================

    [Fact]
    public async Task ShowParentSelectDialogAsync_ReturnsResult()
    {
        var expected = new StringParentSelectResult
        {
            IsConfirmed = true,
            SelectedParent = Meta("p2"),
        };
        SetupResult<StringParentSelectDialog>(DialogResult.Ok(expected));

        var result = await _sut.ShowParentSelectDialogAsync(
            new[] { Meta("1"), Meta("p2") }, currentNode: Meta("1"));

        Assert.NotNull(result);
        Assert.True(result!.IsConfirmed);
        Assert.Equal("p2", result.SelectedParent?.Id);
    }

    [Fact]
    public async Task ShowParentSelectDialogAsync_Canceled_ReturnsNull()
    {
        SetupResult<StringParentSelectDialog>(DialogResult.Cancel());

        var result = await _sut.ShowParentSelectDialogAsync(
            new[] { Meta("1") });

        Assert.Null(result);
    }
}
