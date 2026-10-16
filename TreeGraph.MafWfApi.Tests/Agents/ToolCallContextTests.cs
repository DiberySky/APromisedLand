using TreeGraph.MafWfApi.Agents;
using TreeGraph.Shared.NodeEavSky.Dtos;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Agents;

/// <summary>ToolCallContext.SearchResults 生命周期测试（SSE SearchResults 推送的状态源）。</summary>
public class ToolCallContextTests
{
    [Fact]
    public void SearchResults_可写入读取()
    {
        var ctx = new ToolCallContext();
        var items = new List<SemanticSearchItemDto>
        {
            new("nd-1", "SpaceTree", "节点", "父", "根 / 父 / 节点", "root", 0.9f)
        };

        ctx.SearchResults = items;

        Assert.Same(items, ctx.SearchResults);
    }

    [Fact]
    public void Reset_清空SearchResults与Records()
    {
        var ctx = new ToolCallContext
        {
            SearchResults =
            [
                new SemanticSearchItemDto("nd-1", "SpaceTree", "节点", null, "节点", null, 0.5f)
            ]
        };
        ctx.BeginCall("SemanticSearchNodes", "{}");

        ctx.Reset();

        Assert.Null(ctx.SearchResults);
        Assert.Empty(ctx.Records);
    }
}
