using TreeGraph.MafWfApi.Agents;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Agents;

public class AgentRouterTests
{
    [Theory]
    [InlineData("你好")]
    [InlineData("Hello，在吗？")]
    [InlineData("你是谁？")]
    [InlineData("这个系统能做什么")]
    [InlineData("谢谢你的帮助")]
    [InlineData("请问怎么用")]
    public void ShouldRouteToAssistant_元问题路由到Assistant(string message)
    {
        Assert.True(AgentRouter.ShouldRouteToAssistant(message));
    }

    [Theory]
    [InlineData("列出所有图")]
    [InlineData("第一个图里有哪些节点？")]
    [InlineData("删除名为 test 的边")]
    public void ShouldRouteToAssistant_图查询路由到Graph(string message)
    {
        Assert.False(AgentRouter.ShouldRouteToAssistant(message));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldRouteToAssistant_空输入路由到Graph(string? message)
    {
        Assert.False(AgentRouter.ShouldRouteToAssistant(message!));
    }
}
