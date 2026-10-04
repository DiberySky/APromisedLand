using TreeGraph.Blazor.Shared.Trees.StringTree.Models;
using TreeGraph.Blazor.Shared.Trees.StringTree.Services;
using Xunit;

namespace TreeGraph.Blazor.Shared.Tests.Services;

/// <summary>
/// NoopStringTreeActionHandler：所有写操作必须抛 NotSupportedException，
/// 异常消息应包含方法名与注册引导，便于宿主排障。
/// </summary>
public class NoopStringTreeActionHandlerTests
{
    private readonly NoopStringTreeActionHandler _sut = new();

    [Fact]
    public async Task CreateChildAsync_Throws_WithMethodName()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.CreateChildAsync("p", new StringNodeMeta()));
        Assert.Contains("CreateChildAsync", ex.Message);
        Assert.Contains("IStringTreeActionHandler", ex.Message);
    }

    [Fact]
    public async Task UpdateNodeAsync_Throws_WithMethodName()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.UpdateNodeAsync(new StringNodeMeta()));
        Assert.Contains("UpdateNodeAsync", ex.Message);
    }

    [Fact]
    public async Task DeleteNodeAsync_Throws_WithMethodName()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.DeleteNodeAsync("id"));
        Assert.Contains("DeleteNodeAsync", ex.Message);
    }

    [Fact]
    public async Task MoveNodeAsync_Throws_WithMethodName()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.MoveNodeAsync("id", null));
        Assert.Contains("MoveNodeAsync", ex.Message);
    }

    [Fact]
    public async Task SortChildrenAsync_Throws_WithMethodName()
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => _sut.SortChildrenAsync("p", Array.Empty<string>()));
        Assert.Contains("SortChildrenAsync", ex.Message);
    }

    [Fact]
    public void Message_GuidesUserToRegisterHandler()
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => _sut.DeleteNodeAsync("id").GetAwaiter().GetResult());
        // 引导消息含注册关键词
        Assert.Contains("DI", ex.Message);
        Assert.Contains("注册", ex.Message);
    }
}
