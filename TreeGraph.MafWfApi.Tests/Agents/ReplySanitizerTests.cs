using TreeGraph.MafWfApi.Agents;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Agents;

public class ReplySanitizerTests
{
    [Theory]
    [InlineData("<tool_call>{}</tool_call>")]
    [InlineData("前缀<|tool_call|>后缀")]
    [InlineData("{\"arguments\":{}}")]
    [InlineData("{\"name\":\"GetGraph\"}")]
    [InlineData("{\"name\": \"GetNodes\"}")]
    [InlineData("electronics")] // 含 "ronics"
    public void ContainsToolCallMarkers_命中标记返回true(string text)
    {
        Assert.True(ReplySanitizer.ContainsToolCallMarkers(text));
    }

    [Theory]
    [InlineData("这是正常的回答。")]
    [InlineData("图中的节点数量是 5 个。")]
    public void ContainsToolCallMarkers_正常文本返回false(string text)
    {
        Assert.False(ReplySanitizer.ContainsToolCallMarkers(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ContainsToolCallMarkers_空输入返回false(string? text)
    {
        Assert.False(ReplySanitizer.ContainsToolCallMarkers(text));
    }

    [Fact]
    public void CleanOrEmpty_含标记返回空串()
    {
        Assert.Equal(string.Empty, ReplySanitizer.CleanOrEmpty("<tool_call>x</tool_call>"));
    }

    [Fact]
    public void CleanOrEmpty_正常文本去首尾空白()
    {
        Assert.Equal("你好", ReplySanitizer.CleanOrEmpty("  你好  "));
    }

    [Fact]
    public void CleanOrEmpty_null返回空串()
    {
        Assert.Equal(string.Empty, ReplySanitizer.CleanOrEmpty(null));
    }
}
