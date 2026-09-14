using System.Runtime.CompilerServices;
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
    private readonly IConversationCatalog _catalog;
    private readonly ILogger<MafAgentService> _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private AIAgent? _writerCriticWorkflow;

    public MafAgentService(
        [FromKeyedServices("chat-model")] IChatClient chatClient,
        AgentSessionStore sessionStore,
        IConversationCatalog catalog,
        IOptions<OllamaAgentOptions> options,
        ILoggerFactory loggerFactory)
    {
        var opt = options.Value;
        _sessionStore = sessionStore;
        _catalog = catalog;
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

    // ─────────────────────────────────────────────────────────────
    // 单 Agent 多轮对话
    // ─────────────────────────────────────────────────────────────

    public async Task<AgentReply> ChatAsync(
        string? conversationId,
        string userMessage,
        CancellationToken ct)
    {
        var currentConversationId = conversationId ?? Guid.NewGuid().ToString("N");

        var session = await _sessionStore.GetSessionAsync(
            _generalAssistant, currentConversationId, ct)
            ?? await _generalAssistant.CreateSessionAsync(cancellationToken: ct);

        var response = await _generalAssistant.RunAsync(
            userMessage, session, cancellationToken: ct);

        await _sessionStore.SaveSessionAsync(
            _generalAssistant, currentConversationId, session, ct);

        return new AgentReply(
            ConversationId: currentConversationId,
            AgentName: _generalAssistant.Name ?? "assistant",
            Reply: response.Text,
            MessageCount: response.Messages.Count);
    }

    public ValueTask<IReadOnlyList<string>> ListConversationsAsync(CancellationToken ct)
        => _catalog.ListConversationIdsAsync(ct);

    public ValueTask<bool> ResetConversationAsync(string conversationId, CancellationToken ct)
        => _catalog.DeleteConversationAsync(conversationId, ct);

    public ValueTask<IReadOnlyList<SessionMessage>> GetSessionMessagesAsync(
        string conversationId, CancellationToken ct)
        => _catalog.GetSessionMessagesAsync(conversationId, ct);

    // ─────────────────────────────────────────────────────────────
    // Writer-Critic 工作流（非流式）
    // ─────────────────────────────────────────────────────────────

    public async Task<WorkflowReply> RunWriterCriticAsync(
        string topic, CancellationToken ct)
    {
        var workflowAgent = await GetWorkflowAgentAsync(ct);
        var session = await workflowAgent.CreateSessionAsync(cancellationToken: ct);

        var response = await workflowAgent.RunAsync(
            topic, session, cancellationToken: ct);

        var steps = response.Messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => new WorkflowStep(
                Agent: m.AuthorName ?? "unknown",
                Text: m.Text))
            .ToList();

        if (steps.Count == 0 && !string.IsNullOrWhiteSpace(response.Text))
            steps.Add(new WorkflowStep("WriterCritic", response.Text));

        return new WorkflowReply(
            Topic: topic,
            FinalAnswer: response.Text,
            Steps: steps);
    }

    // ─────────────────────────────────────────────────────────────
    // Writer-Critic 工作流（流式）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 流式运行 Writer-Critic 工作流，逐块产出增量文本。
    /// 每个事件携带 Agent 名、增量文本和是否最终帧。
    /// </summary>
    public async IAsyncEnumerable<WorkflowStreamEvent> RunWriterCriticStreamAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var workflowAgent = await GetWorkflowAgentAsync(ct);
        var session = await workflowAgent.CreateSessionAsync(cancellationToken: ct);

        await foreach (var update in workflowAgent
            .RunStreamingAsync(topic, session, cancellationToken: ct))
        {
            // 提取增量文本
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta) && update.Contents is { Count: > 0 })
            {
                delta = string.Concat(update.Contents
                    .OfType<TextContent>()
                    .Select(c => c.Text));
            }

            if (string.IsNullOrEmpty(delta))
                continue;

            // AgentResponseUpdate 使用 FinishReason 属性表示结束原因。
            // 当 FinishReason 为 ChatFinishReason.Stop 时，表示当前是最终帧。
            var isFinal = update.FinishReason == ChatFinishReason.Stop;

            // AuthorName 声明为 string?，显式判空以避免可空性分析警告
            var authorName = update.AuthorName;
            if (string.IsNullOrEmpty(authorName))
                authorName = "unknown";

            yield return new WorkflowStreamEvent(
                Agent: authorName,
                Delta: delta,
                IsFinal: isFinal);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 私有辅助
    // ─────────────────────────────────────────────────────────────

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