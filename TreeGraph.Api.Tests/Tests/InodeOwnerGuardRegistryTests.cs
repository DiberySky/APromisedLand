using TreeGraph.Api.NodeEavSky.Services;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// InodeOwnerGuardRegistry 聚合逻辑测试（纯内存，不依赖数据库）。
/// </summary>
public class InodeOwnerGuardRegistryTests
{
    private sealed class StubGuard(string nodeType, bool exists) : IInodeOwnerGuard
    {
        public string NodeType { get; } = nodeType;

        public Task<bool> NodeExistsAsync(string inodeId, CancellationToken ct = default)
            => Task.FromResult(exists);
    }

    [Fact]
    public async Task NoGuards_ExternalMode_Passes()
    {
        var registry = new InodeOwnerGuardRegistry([]);
        await registry.EnsureNodeExistsAsync("any-node-id");
    }

    [Fact]
    public async Task AnyGuardConfirmsExists_Passes()
    {
        var registry = new InodeOwnerGuardRegistry(
        [
            new StubGuard("A", false),
            new StubGuard("B", true),
            new StubGuard("C", false)
        ]);

        await registry.EnsureNodeExistsAsync("node-1");
    }

    [Fact]
    public async Task AllGuardsDeny_Throws()
    {
        var registry = new InodeOwnerGuardRegistry(
        [
            new StubGuard("A", false),
            new StubGuard("B", false)
        ]);

        await Assert.ThrowsAsync<EavValidationException>(
            () => registry.EnsureNodeExistsAsync("ghost-node"));
    }

    // ============================================================
    // Resolve(nodeType)：按类型精确解析守卫（EdgeSky 起点终点校验依赖）
    // ============================================================

    [Fact]
    public void Resolve_RegisteredNodeType_ReturnsGuard()
    {
        var guardB = new StubGuard("B", true);
        var registry = new InodeOwnerGuardRegistry(
        [
            new StubGuard("A", true),
            guardB
        ]);

        Assert.Same(guardB, registry.Resolve("B"));
    }

    [Fact]
    public void Resolve_UnregisteredNodeType_ReturnsNull()
    {
        var registry = new InodeOwnerGuardRegistry([new StubGuard("A", true)]);

        Assert.Null(registry.Resolve("GhostType"));
    }

    [Fact]
    public void Resolve_NoGuards_ReturnsNull()
    {
        var registry = new InodeOwnerGuardRegistry([]);

        Assert.Null(registry.Resolve("UnitTree"));
    }

    [Fact]
    public void Resolve_IsCaseSensitive()
    {
        var registry = new InodeOwnerGuardRegistry([new StubGuard("UnitTree", true)]);

        Assert.Null(registry.Resolve("unittestree"));
        Assert.NotNull(registry.Resolve("UnitTree"));
    }

    // ============================================================
    // ResolveNodeNamesAsync：跨守卫批量解析显示名（边端点展示依赖）
    // ============================================================

    /// <summary>覆盖名字解析的桩守卫：按内置字典回名。</summary>
    private sealed class NamedStubGuard(
        string nodeType, IReadOnlyDictionary<string, string> names) : IInodeOwnerGuard
    {
        public string NodeType { get; } = nodeType;

        public Task<bool> NodeExistsAsync(string inodeId, CancellationToken ct = default)
            => Task.FromResult(names.ContainsKey(inodeId));

        public Task<Dictionary<string, string>> GetNodeNamesAsync(
            IReadOnlyCollection<string> inodeIds, CancellationToken ct = default)
            => Task.FromResult(inodeIds
                .Where(names.ContainsKey)
                .ToDictionary(id => id, id => names[id], StringComparer.Ordinal));
    }

    [Fact]
    public async Task ResolveNodeNames_AggregatesAcrossGuards()
    {
        var registry = new InodeOwnerGuardRegistry(
        [
            new NamedStubGuard("SpaceTree",
                new Dictionary<string, string> { ["n-1"] = "武汉供电段" }),
            new NamedStubGuard("UnitTree",
                new Dictionary<string, string> { ["n-2"] = "检修班 【JX】" })
        ]);

        var names = await registry.ResolveNodeNamesAsync(["n-1", "n-2", "ghost"]);

        Assert.Equal("武汉供电段", names["n-1"]);
        Assert.Equal("检修班 【JX】", names["n-2"]);
        // 未认领 id 不出现在结果中
        Assert.False(names.ContainsKey("ghost"));
    }

    [Fact]
    public async Task ResolveNodeNames_DefaultGuard_ReturnsEmpty()
    {
        // StubGuard 未覆盖 GetNodeNamesAsync（接口默认空实现），不报错、不贡献名字
        var registry = new InodeOwnerGuardRegistry([new StubGuard("A", true)]);

        var names = await registry.ResolveNodeNamesAsync(["n-1"]);

        Assert.Empty(names);
    }

    [Fact]
    public async Task ResolveNodeNames_NoGuards_ReturnsEmpty()
    {
        var registry = new InodeOwnerGuardRegistry([]);

        var names = await registry.ResolveNodeNamesAsync(["n-1"]);

        Assert.Empty(names);
    }
}
