using TreeGraph.Api.NodeEavSky.Services;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// InodeOwnerGuardRegistry 聚合逻辑测试（纯内存，不依赖数据库）。
/// </summary>
public class InodeOwnerGuardRegistryTests
{
    private sealed class StubGuard(bool exists) : IInodeOwnerGuard
    {
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
            new StubGuard(false),
            new StubGuard(true),
            new StubGuard(false)
        ]);

        await registry.EnsureNodeExistsAsync("node-1");
    }

    [Fact]
    public async Task AllGuardsDeny_Throws()
    {
        var registry = new InodeOwnerGuardRegistry(
        [
            new StubGuard(false),
            new StubGuard(false)
        ]);

        await Assert.ThrowsAsync<EavValidationException>(
            () => registry.EnsureNodeExistsAsync("ghost-node"));
    }
}
