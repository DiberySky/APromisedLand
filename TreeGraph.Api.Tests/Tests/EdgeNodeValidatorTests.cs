using TreeGraph.Api.EdgeSky.Services;
using TreeGraph.Api.NodeEavSky.Services;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// EdgeNodeValidator 纯内存单元测试：
/// 起点/终点节点必须是 Inode 宿主（有 IInodeOwnerGuard 认领的节点类型）。
/// </summary>
public class EdgeNodeValidatorTests
{
    private sealed class StubGuard(string nodeType, bool exists) : IInodeOwnerGuard
    {
        public string NodeType { get; } = nodeType;

        public Task<bool> NodeExistsAsync(string inodeId, CancellationToken ct = default)
            => Task.FromResult(exists);
    }

    [Fact]
    public async Task EmptyNodeType_Passes()
    {
        // IsGlobal 边不限定节点类型，不校验
        var validator = new EdgeNodeValidator(new InodeOwnerGuardRegistry([]));

        await validator.EnsureNodeAsync(null, "any-node");
        await validator.EnsureNodeAsync("", "any-node");
    }

    [Fact]
    public async Task UnclaimedNodeType_Throws()
    {
        var validator = new EdgeNodeValidator(
            new InodeOwnerGuardRegistry([new StubGuard("UnitTree", true)]));

        var ex = await Assert.ThrowsAsync<EavValidationException>(
            () => validator.EnsureNodeAsync("GhostType", "node-1"));

        Assert.Equal("GhostType", ex.Errors[0].Field);
        Assert.Contains("未实现 Inode", ex.Errors[0].Message);
    }

    [Fact]
    public async Task ClaimedNodeType_NodeMissing_Throws()
    {
        var validator = new EdgeNodeValidator(
            new InodeOwnerGuardRegistry([new StubGuard("UnitTree", false)]));

        var ex = await Assert.ThrowsAsync<EavValidationException>(
            () => validator.EnsureNodeAsync("UnitTree", "ghost-node"));

        Assert.Contains("不存在于 UnitTree", ex.Errors[0].Message);
    }

    [Fact]
    public async Task ClaimedNodeType_NodeExists_Passes()
    {
        var validator = new EdgeNodeValidator(
            new InodeOwnerGuardRegistry([new StubGuard("UnitTree", true)]));

        await validator.EnsureNodeAsync("UnitTree", "node-1");
    }
}
