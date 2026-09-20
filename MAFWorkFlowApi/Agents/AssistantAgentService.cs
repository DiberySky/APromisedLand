using MAFWorkFlowApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 通用助手 Agent：处理问候、能力询问等元问题。
/// 无工具，无状态（每次全新 session，不持久化）。
/// </summary>
public sealed class AssistantAgentService
{
    private readonly AIAgent _assistantAgent;
    private readonly ILogger<AssistantAgentService> _logger;

    public AssistantAgentService(
        [FromKeyedServices("chat-model")] IChatClient chatClient,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<AssistantAgentService>();

        _assistantAgent = chatClient.AsAIAgent(
            instructions: BuildInstructions(),
            name: "Assistant");
    }

    public async Task<AgentReply> ChatAsync(
        string? conversationId,
        string userMessage,
        CancellationToken ct)
    {
        var currentConversationId = conversationId ?? Guid.NewGuid().ToString("N");

        // ★ 无状态：每次都是全新 session，不与 GraphAgent 共享 Redis key
        var session = await _assistantAgent.CreateSessionAsync(cancellationToken: ct);

        var response = await _assistantAgent.RunAsync(
            userMessage, session, cancellationToken: ct);

        _logger.LogInformation("[AssistantAgent] 处理元问题");

        return new AgentReply(
            ConversationId: currentConversationId,
            AgentName: "Assistant",
            Reply: response.Text?.Trim() ?? "（模型未返回内容）",
            MessageCount: response.Messages.Count,
            ToolsInvoked: new List<string>(),
            ToolCallDetails: new List<ToolCallDetailDto>());
    }

    private static string BuildInstructions() => """
                                                 你是一个友好的助手，是图数据查询系统的一部分。

                                                 你的职责：
                                                 1. 回答关于你自身能力的询问。
                                                 2. 引导用户正确地提问。
                                                 3. 回应用户的问候和感谢。

                                                 关于你的能力：
                                                 - 你可以查询图数据库中的节点和关系。
                                                 - 支持：查找节点、查询邻居、找关系、找路径、探索子图、关键词搜索等。
                                                 - 用户提问时建议带上图名（如 "TestGraph"）和节点名（如 "根节点C"）。

                                                 回答风格：
                                                 - 简洁、友好、使用中文。
                                                 - 当用户问"你能做什么"时，用简短列表说明，不要长篇大论。
                                                 - 不要编造你做不到的功能。
                                                 """;
}