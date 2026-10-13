using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TreeGraph.Api.Tests.Fixtures;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// 树 /move 集成测试（真实 PostgreSQL via testcontainers）。
/// 覆盖「移动到非空父节点」路径 —— IsDescendantAsync 的 Npgsql 递归 CTE
/// （InMemory 无法执行原生 SQL，UnitTreeServiceTests/CategoryTreeServiceTests 只覆盖移到根）。
/// 递归 CTE 结果列须带 AS "Value" 别名且无尾部分号（SqlQueryRaw&lt;bool&gt; 会被
/// EF Core 包装为 SELECT s."Value" FROM (...) AS s）。
/// </summary>
public class TreeMoveIntegrationTests : IntegrationTestBase
{
    public TreeMoveIntegrationTests(EavApiFactory factory) : base(factory) { }

    /// <summary>创建节点并返回 Id。parentId 为 null 时挂在根。</summary>
    private async Task<string> CreateNodeAsync(string tree, string text, string? parentId)
    {
        // 显式给 Id：TreeNodeDto.Id 默认 string.Empty 而非 null，
        // 服务端 `nodeDto.Id ?? Guid.NewGuid()` 对空字符串不会再生成新值，会导致 PK 冲突。
        var id = Guid.NewGuid().ToString();
        var resp = await Client.PostAsJsonAsync(tree, new
        {
            id,
            text,
            parentId,
            sortOrder = 0
        });
        resp.EnsureSuccessStatusCode();
        return id;
    }

    [Theory]
    [InlineData("/UnitTree")]
    [InlineData("/CategoryTree")]
    [InlineData("/SpaceTree")]
    public async Task Move_ToNonRootParent_Works(string tree)
    {
        var parent = await CreateNodeAsync(tree, $"P_{Guid.NewGuid():N}", null);
        var target = await CreateNodeAsync(tree, $"T_{Guid.NewGuid():N}", null);
        var child = await CreateNodeAsync(tree, $"C_{Guid.NewGuid():N}", parent);

        var resp = await Client.PostAsync(
            $"{tree}/move?nodeId={child}&newParentId={target}", null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // 断言 child 已挂到 target 下
        var children = (await Client
            .GetFromJsonAsync<ApiResponse<List<JsonElement>>>($"{tree}/children/{target}"))!.Data;
        Assert.NotNull(children);
        Assert.Contains(children, c => c.GetProperty("id").GetString() == child);
    }

    [Theory]
    [InlineData("/UnitTree")]
    [InlineData("/CategoryTree")]
    [InlineData("/SpaceTree")]
    public async Task Move_ToOwnDescendant_Returns400(string tree)
    {
        var parent = await CreateNodeAsync(tree, $"P_{Guid.NewGuid():N}", null);
        var child = await CreateNodeAsync(tree, $"C_{Guid.NewGuid():N}", parent);

        // 把 parent 移到其后代 child 下：循环检测（递归 CTE）应拒绝
        var resp = await Client.PostAsync(
            $"{tree}/move?nodeId={parent}&newParentId={child}", null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
