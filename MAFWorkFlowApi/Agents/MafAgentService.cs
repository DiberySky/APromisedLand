using MAFWorkFlowApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MAFWorkFlowApi.Agents;

/// <summary>
/// 对 MAF 的封装：负责构造 Ollama-backed ChatClientAgent、组装 Sequential Workflow，
/// 并对外暴露两个业务动作：单轮问答、跑"写作→审校"工作流。
/// </summary>
public sealed class MafAgentService
{
    private readonly AIAgent _generalAssistant;
    private readonly AIAgent _writer;
    private readonly AIAgent _critic;
    private readonly ILogger<MafAgentService> _logger;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private AIAgent? _writerCriticWorkflow;

    public MafAgentService(
        [FromKeyedServices("chat-model")] IChatClient chatClient,
        IOptions<OllamaAgentOptions> options,
        ILoggerFactory loggerFactory)
    {
        var opt = options.Value;
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

    /// <summary>单 Agent 问答。</summary>
    public async Task<AgentReply> ChatAsync(string userMessage, CancellationToken ct)
    {
        // ✅ AgentSession 不实现 IAsyncDisposable，直接使用变量即可
        var session = await _generalAssistant.CreateSessionAsync(
            cancellationToken: ct);

        var response = await _generalAssistant.RunAsync(
            userMessage, session, cancellationToken: ct);

        return new AgentReply(
            // ✅ AIAgent.Name 可能为 null，提供回退值
            AgentName: _generalAssistant.Name ?? "assistant",
            // ✅ AgentResponse.Text 非空，无需 ??
            Reply: response.Text,
            MessageCount: response.Messages.Count);
    }

    /// <summary>跑 Sequential 工作流：Writer -> Critic。</summary>
    public async Task<WorkflowReply> RunWriterCriticAsync(
        string topic, CancellationToken ct)
    {
        var workflowAgent = await GetWorkflowAgentAsync(ct);

        // ✅ 同上，不使用 await using
        var session = await workflowAgent.CreateSessionAsync(
            cancellationToken: ct);

        var response = await workflowAgent.RunAsync(
            topic, session, cancellationToken: ct);

        var steps = response.Messages
            .Select(m => new WorkflowStep(
                // ✅ ChatMessage.AuthorName 可能为 null，提供回退值
                Agent: m.AuthorName ?? "unknown",
                // ✅ ChatMessage.Text 非空，无需 ??
                Text: m.Text))
            .ToList();

        return new WorkflowReply(
            Topic: topic,
            // ✅ AgentResponse.Text 非空，无需 ??
            FinalAnswer: response.Text,
            Steps: steps);
    }

    /// <summary>
    /// 惰性初始化 WriterCritic 工作流。
    /// 使用 SemaphoreSlim 而非 Lazy&lt;Task&gt;：允许失败后重试。
    /// </summary>
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

                // ✅ MAF 1.21：AsAIAgent 扩展方法
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