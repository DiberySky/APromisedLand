using MAFWorkFlowApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 对 MAF 的封装：负责构造 Ollama-backed ChatClientAgent、组装 Sequential Workflow，
/// 并通过 AgentSessionStore（Redis）实现持久化多轮会话。
/// </summary>
public sealed class MafAgentService
{
    private readonly AIAgent _generalAssistant;
    private readonly AIAgent _writer;
    private readonly AIAgent _critic;
    private readonly AgentSessionStore _sessionStore;
    private readonly ILogger<MafAgentService> _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private AIAgent? _writerCriticWorkflow;

    public MafAgentService(
        [FromKeyedServices("chat-model")] IChatClient chatClient,
        AgentSessionStore sessionStore,
        IOptions<OllamaAgentOptions> options,
        ILoggerFactory loggerFactory)
    {
        var opt = options.Value;
        _sessionStore = sessionStore;
        _logger = loggerFactory.CreateLogger<MafAgentService>();

        _logger.LogInformation(
            "Initializing MafAgentService, model = {Model}", opt.ModelId);

        _generalAssistant = chatClient.AsAIAgent(
            instructions: opt.AssistantInstructions,
            name: "Assistant");

        _writer = chatClient.AsAIAgent(
            instructions: opt.WriterInstructions,
            name: "Writer");

        _critic = chatClient.AsAIAgent(
            instructions: opt.CriticInstructions,
            name: "Critic");
    }

    /// <summary>
    /// 多轮会话：加载 → 运行 → 保存。
    /// </summary>
    public async Task<AgentReply> ChatAsync(
        string? conversationId,
        string userMessage,
        CancellationToken ct)
    {
        // 1. 生成或沿用 conversationId
        var currentConversationId = conversationId ?? Guid.NewGuid().ToString("N");

        // 2. 加载会话：如果不存在，则创建新会话
        var session = await _sessionStore.GetSessionAsync(
            _generalAssistant, currentConversationId, ct)
            ?? await _generalAssistant.CreateSessionAsync(cancellationToken: ct);

        // 3. 使用该会话运行
        var response = await _generalAssistant.RunAsync(
            userMessage, session, cancellationToken: ct);

        // 4. 保存更新后的会话（包含最新对话历史）
        await _sessionStore.SaveSessionAsync(
            _generalAssistant, currentConversationId, session, ct);

        return new AgentReply(
            ConversationId: currentConversationId,
            AgentName: _generalAssistant.Name ?? "assistant",
            Reply: response.Text,
            MessageCount: response.Messages.Count);
    }

    /// <summary>跑 Sequential 工作流：Writer -> Critic（工作流暂不持久化）。</summary>
    public async Task<WorkflowReply> RunWriterCriticAsync(
        string topic, CancellationToken ct)
    {
        var workflowAgent = await GetWorkflowAgentAsync(ct);

        var session = await workflowAgent.CreateSessionAsync(
            cancellationToken: ct);

        var response = await workflowAgent.RunAsync(
            topic, session, cancellationToken: ct);

        var steps = response.Messages
            .Select(m => new WorkflowStep(
                Agent: m.AuthorName ?? "unknown",
                Text: m.Text))
            .ToList();

        return new WorkflowReply(
            Topic: topic,
            FinalAnswer: response.Text,
            Steps: steps);
    }

    private async Task<AIAgent> GetWorkflowAgentAsync(CancellationToken ct)
    {
        if (_writerCriticWorkflow is not null)
            return _writerCriticWorkflow;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_writerCriticWorkflow is null)
            {
                _logger.LogInformation(
                    "Initializing WriterCritic workflow agent");

                var workflow = new WorkflowBuilder(_writer)
                    .AddEdge(_writer, _critic)
                    .WithName("WriterCriticPipeline")
                    .WithDescription("先由 Writer 出初稿，再由 Critic 直接产出终稿")
                    .Build();

                _writerCriticWorkflow = workflow.AsAIAgent(
                    id: "writer-critic-workflow",
                    name: "WriterCritic");
            }

            return _writerCriticWorkflow;
        }
        finally
        {
            _initLock.Release();
        }
    }
}