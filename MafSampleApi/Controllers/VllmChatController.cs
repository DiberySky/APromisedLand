using MafSampleApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MafSampleApi.Controllers;

/// <summary>
/// vLLM（Qwen3-4B-AWQ）专用聊天端点。
///
/// 特点：
/// - 非流式：一次返回完整回答
/// - 强制 /no_think：关闭 Qwen3 思考模式，推理更快、响应更短
/// - 不经过 MAF Agent / SessionStore，直接调用 IChatClient
/// - 无对话历史，单次问答（无状态）
///
/// 路由：POST /api/vllm/chat
/// </summary>
[ApiController]
[Route("api/vllm/chat")]
[Produces("application/json")]
public sealed class VllmChatController(
    IChatClient chatClient,
    IOptions<AgentOptions> agentOptions,
    ILogger<VllmChatController> logger) : ControllerBase
{
    private readonly AgentOptions _options = agentOptions.Value;

    /// <summary>Qwen3 关闭思考模式指令，必须追加到 user 消息末尾。</summary>
    private const string NoThinkHint = " /no_think";

    private const int DefaultMaxOutputTokens = 2048;

    // ─── POST /api/vllm/chat ────────────────────────────────
    [HttpPost]
    public async Task<ActionResult<VllmChatResponseDto>> ChatAsync(
        [FromBody] VllmChatRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "message is required." });

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? _options.ChatModel
            : request.Model;

        var systemPrompt = string.IsNullOrWhiteSpace(request.SystemPrompt)
            ? _options.SystemPrompt
            : request.SystemPrompt;

        // ★ Qwen3 no_think 必须紧贴 user 消息末尾，不能被空白/换行隔开
        var userMessage = AppendNoThink(request.Message);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User,   userMessage),
        };

        var maxTokens = request.MaxOutputTokens is > 0
            ? request.MaxOutputTokens.Value
            : DefaultMaxOutputTokens;

        var chatOptions = new ChatOptions
        {
            ModelId         = model,
            MaxOutputTokens = maxTokens,
        };

        logger.LogInformation(
            "vLLM chat: model={Model}, msgLen={Len}, maxTokens={Max}, noThink=true",
            model, userMessage.Length, maxTokens);

        try
        {
            var response = await chatClient.GetResponseAsync(messages, chatOptions, ct);

            return Ok(new VllmChatResponseDto
            {
                Model   = model,
                Content = response.Text ?? string.Empty,
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 客户端断开：无接收方，静默退出
            logger.LogInformation("vLLM chat client disconnected: model={Model}", model);
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "vLLM chat failed: model={Model}", model);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = ex.Message, model });
        }
    }

    // ─── 工具 ───────────────────────────────────────────────
    private static string AppendNoThink(string message)
    {
        if (message.Contains("/no_think", StringComparison.OrdinalIgnoreCase))
            return message;
        return message.TrimEnd() + NoThinkHint;
    }
}