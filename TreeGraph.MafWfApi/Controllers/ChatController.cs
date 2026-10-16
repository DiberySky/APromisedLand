using System.ComponentModel.DataAnnotations;
using TreeGraph.MafWfApi.Agents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace TreeGraph.MafWfApi.Controllers;

/// <summary>
/// 无工具单轮对话入口（直连 vLLM，不走 Agent / 会话持久化）。
/// 路由前缀：/api/chat
/// </summary>
[ApiController]
[Route("api/chat")]
[Produces("application/json")]
public sealed class ChatController(
    IChatClient chatClient,
    IOptions<AgentOptions> options,
    ILogger<ChatController> logger) : ControllerBase
{
    private readonly AgentOptions _options = options.Value;

    [HttpPost]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponseDto>> Chat(
        [FromBody] ChatRequestDto request,
        CancellationToken ct)
    {
        logger.LogInformation("Chat request, MessageLength={Len}", request.Message.Length);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, _options.SystemPrompt),
            new(ChatRole.User, request.Message)
        };

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);

        return Ok(new ChatResponseDto(
            Reply: response.Text?.Trim() ?? "（模型未返回内容）",
            Model: _options.ChatModel));
    }
}

public sealed class ChatRequestDto
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

public sealed record ChatResponseDto(string Reply, string Model);
