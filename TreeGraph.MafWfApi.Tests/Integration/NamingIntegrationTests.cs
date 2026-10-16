using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TreeGraph.MafWfApi.Tests.Integration;

/// <summary>
/// /api/naming/generate 集成测试：复用 MafWfApiFactory（EchoChatClient 返回"集成测试回答"），
/// 重点验证 ApiEnvelopeFilter 对新控制器的信封包装一致性。
/// </summary>
public class NamingIntegrationTests(MafWfApiFactory factory)
    : IClassFixture<MafWfApiFactory>
{
    private static readonly JsonSerializerOptions JsonOpts =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task generate_正常请求返回200且响应被信封包装()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/naming/generate",
            new { text = "用户ID" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        // 信封三件套
        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("message").ValueKind);

        // data 内是 NamingResult（web 默认 camelCase）
        var data = root.GetProperty("data");
        Assert.Equal("用户ID", data.GetProperty("source").GetString());
        Assert.True(data.TryGetProperty("snakeCase", out var snake));
        Assert.True(data.TryGetProperty("pascalCase", out var pascal));
        Assert.False(string.IsNullOrWhiteSpace(snake.GetString()));
        Assert.False(string.IsNullOrWhiteSpace(pascal.GetString()));
        Assert.Equal(JsonValueKind.Array, data.GetProperty("warnings").ValueKind);
    }

    [Fact]
    public async Task generate_空文本返回400且同样被信封包装()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/naming/generate",
            new { text = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.False(root.GetProperty("success").GetBoolean());
        // 信封对 400 提取 ValidationProblemDetails.title 作为 message
        Assert.Equal("One or more validation errors occurred.",
            root.GetProperty("message").GetString());
    }

    [Fact]
    public async Task generate_超长文本返回400()
    {
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync("/api/naming/generate",
            new { text = new string('长', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
