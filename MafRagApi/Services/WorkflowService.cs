using System.Runtime.CompilerServices;
using MafRagApi.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace MafRagApi.Services;

/// <summary>
/// MAF 工作流编排服务。
/// 基于 Microsoft.Agents.AI.Workflows 的 WorkflowBuilder 构建多 Agent 顺序工作流，
/// 当前提供 Writer-Critic（撰写-审稿）两阶段流水线。
/// </summary>
public sealed class WorkflowService
{
    private readonly IChatClient _chatClient;
    private readonly AgentOptions _options;
    private readonly ILogger<WorkflowService> _logger;

    private readonly AIAgent _writer;
    private readonly AIAgent _critic;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private AIAgent? _writerCriticWorkflow;

    public WorkflowService(
        IChatClient chatClient,
        IOptions<AgentOptions> options,
        ILogger<WorkflowService> logger)
    {
        _chatClient = chatClient;
        _options = options.Value;
        _logger = logger;

        // 限制每步输出长度，避免本地模型推理过慢
        var writerOptions = new ChatClientAgentOptions
        {
            Name = "Writer",
            ChatOptions = new ChatOptions
            {
                Instructions = "你是一名技术文案撰写专家。根据用户给出的主题，写一段结构清晰、信息密度高的中文初稿，不超过 100 字。只输出正文。",
                MaxOutputTokens = 256,
                Temperature = 0.7f,
            },
        };
        var criticOptions = new ChatClientAgentOptions
        {
            Name = "Critic",
            ChatOptions = new ChatOptions
            {
                Instructions = "你是一名严格的技术编辑。阅读下面这篇初稿，挑出事实性、逻辑和表达问题，并直接输出一版润色后的中文终稿。只输出终稿正文，不要解释你改了什么。",
                MaxOutputTokens = 256,
                Temperature = 0.5f,
            },
        };

        _writer = new ChatClientAgent(_chatClient, writerOptions);
        _critic = new ChatClientAgent(_chatClient, criticOptions);
    }

    // ─────────────────────────────────────────────────────────────
    //  Writer-Critic 工作流（非流式）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 非流式运行 Writer-Critic 工作流，返回各步骤产出与最终答案。
    /// </summary>
    public async Task<WorkflowRunResponseDto> RunWriterCriticAsync(
        string topic, CancellationToken ct)
    {
        _logger.LogInformation("Running writer-critic workflow for topic: {Topic}", topic);

        var workflowAgent = await GetWorkflowAgentAsync(ct);
        var session = await workflowAgent.CreateSessionAsync(cancellationToken: ct);

        var response = await workflowAgent.RunAsync(topic, session, cancellationToken: ct);

        var steps = response.Messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => new WorkflowStepDto
            {
                Agent = m.AuthorName ?? "unknown",
                Text = m.Text,
            })
            .ToList();

        if (steps.Count == 0 && !string.IsNullOrWhiteSpace(response.Text))
            steps.Add(new WorkflowStepDto { Agent = "WriterCritic", Text = response.Text });

        return new WorkflowRunResponseDto
        {
            Topic = topic,
            FinalAnswer = response.Text,
            Steps = steps,
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  Writer-Critic 工作流（流式）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 流式运行 Writer-Critic 工作流，逐块产出增量文本。
    /// </summary>
    public async IAsyncEnumerable<WorkflowStreamEventDto> RunWriterCriticStreamAsync(
        string topic,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Streaming writer-critic workflow for topic: {Topic}", topic);

        var workflowAgent = await GetWorkflowAgentAsync(ct);
        var session = await workflowAgent.CreateSessionAsync(cancellationToken: ct);

        await foreach (var update in workflowAgent
            .RunStreamingAsync(topic, session, cancellationToken: ct)
            .WithCancellation(ct))
        {
            var delta = update.Text;
            if (string.IsNullOrEmpty(delta) && update.Contents is { Count: > 0 })
            {
                delta = string.Concat(update.Contents
                    .OfType<TextContent>()
                    .Select(c => c.Text));
            }

            if (string.IsNullOrEmpty(delta))
                continue;

            var isFinal = update.FinishReason == ChatFinishReason.Stop;

            var authorName = update.AuthorName;
            if (string.IsNullOrEmpty(authorName))
                authorName = "unknown";

            yield return new WorkflowStreamEventDto
            {
                Agent = authorName,
                Delta = delta,
                IsFinal = isFinal,
            };
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  私有辅助
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 懒加载构建 Writer-Critic 工作流 Agent（线程安全）。
    /// 使用 WorkflowBuilder 将 Writer → Critic 串联为顺序工作流。
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
                _logger.LogInformation("Initializing WriterCritic workflow agent");

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
