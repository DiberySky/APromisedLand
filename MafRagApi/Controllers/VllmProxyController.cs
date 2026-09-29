using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace MafRagApi.Controllers;

/// <summary>
/// 本地代理：在请求体注入 chat_template_kwargs.enable_thinking=false 后转发到 vLLM。
/// AsIChatClient() 适配器绕过 OpenAI SDK pipeline，只能用代理注入。
/// </summary>
[ApiController]
[Route("proxy/v1")]
public class VllmProxyController : ControllerBase
{
    private readonly HttpClient _upstream;
    private readonly ILogger<VllmProxyController> _logger;

    public VllmProxyController(IHttpClientFactory factory, ILogger<VllmProxyController> logger)
    {
        _upstream = factory.CreateClient("vllm-upstream");
        _logger = logger;
    }

    [HttpPost("chat/completions")]
    public async Task ChatCompletions()
    {
        // 读取原始请求体
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        var node = JsonNode.Parse(body);
        if (node is JsonObject root)
        {
            // 注入 chat_template_kwargs.enable_thinking=false
            if (!root.TryGetPropertyValue("chat_template_kwargs", out _))
                root["chat_template_kwargs"] = new JsonObject();
            if (root["chat_template_kwargs"] is JsonObject kw)
                kw["enable_thinking"] = false;
        }
        var modifiedBody = node?.ToJsonString() ?? body;

        // 转发到 vLLM
        var upstreamReq = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(modifiedBody, Encoding.UTF8, "application/json")
        };

        // 传递 Authorization
        if (Request.Headers.TryGetValue("Authorization", out var auth))
            upstreamReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.ToString().Replace("Bearer ", ""));

        // 判断是否流式
        var isStream = node?["stream"]?.GetValue<bool>() ?? false;

        var upstreamResp = await _upstream.SendAsync(upstreamReq, isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, HttpContext.RequestAborted);

        // 复制状态码和 headers
        Response.StatusCode = (int)upstreamResp.StatusCode;
        foreach (var h in upstreamResp.Headers)
            Response.Headers[h.Key] = h.Value.ToArray();
        foreach (var h in upstreamResp.Content.Headers)
            Response.Headers[h.Key] = h.Value.ToArray();
        Response.Headers.Remove("transfer-encoding");

        if (isStream)
        {
            // 流式：逐块复制 SSE
            await using var upstreamStream = await upstreamResp.Content.ReadAsStreamAsync(HttpContext.RequestAborted);
            await using var responseStream = Response.Body;
            await upstreamStream.CopyToAsync(responseStream, HttpContext.RequestAborted);
            await Response.Body.FlushAsync(HttpContext.RequestAborted);
        }
        else
        {
            // 非流式：直接复制
            var respBody = await upstreamResp.Content.ReadAsByteArrayAsync(HttpContext.RequestAborted);
            await Response.Body.WriteAsync(respBody, HttpContext.RequestAborted);
        }
    }
}
