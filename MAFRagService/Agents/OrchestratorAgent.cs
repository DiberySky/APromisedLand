using MAFRagService.Memory;
using MAFRagService.Models;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Agents;

/// <summary>
/// 编排 Agent：负责把一次问答拆解为「意图分析 → 检索 → 图推理 → 生成」四步。
///
/// <para><b>并行策略：</b></para>
/// <list type="bullet">
///   <item>意图分析必须先完成（后续步骤依赖 QueryIntent）；</item>
///   <item>向量检索与图推理互不依赖，通过 Task.WhenAll 并行；</item>
///   <item>生成依赖前两者的结果，串行执行。</item>
/// </list>
///
/// <para><b>FeatureFlags 兼容：</b></para>
/// <list type="bullet">
///   <item>Graph 关闭时 <see cref="GraphReasonerAgent"/> 未注册，
///         通过 <c>IServiceProvider.GetService</c> 可空获取；</item>
///   <item>会话记忆弱依赖：只有 <see cref="IMemoryStore"/> 实现为
///         <see cref="INebulaGraphMemoryStore"/> 时才写入。</item>
/// </list>
///
/// <para><b>返回值变更：</b></para>
/// 从 <c>Task&lt;string&gt;</c> 改为 <c>Task&lt;AskResult&gt;</c>，
/// 让调用方能拿到 <c>Sources</c>，便于调试与展示。
/// </summary>
public class OrchestratorAgent : Agent
{
    private readonly IServiceProvider _services;
    private readonly IMemoryStore _memory;
    private readonly ILogger<OrchestratorAgent> _logger;

    public OrchestratorAgent(
        IServiceProvider services,
        IMemoryStore memory,
        ILogger<OrchestratorAgent> logger)
    {
        _services = services;
        _memory   = memory;
        _logger   = logger;
    }

    [AgentFunction("AskQuestion")]
    public async Task<AskResult> AskQuestion(
        [Parameter(Description = "User's question")] string question,
        [Parameter(Description = "Tenant ID")] string tenant,
        [Parameter(Description = "Version filter")] string? version = null,
        [Parameter(Description = "Use graph reasoning?")] bool useGraph = true,
        [Parameter(Description = "Top K results")] int topK = 5)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var ct = cts.Token;

        // ---------- 1. 意图分析（必须最先完成） ----------
        var analyzer = _services.GetRequiredService<QueryAnalyzerAgent>();
        var intent   = await analyzer.AnalyzeAsync(question, tenant, ct);

        // ---------- 2. 检索 + 图推理（并行） ----------
        var retriever = _services.GetRequiredService<RetrieverAgent>();

        // Graph 关闭时返回 null，不抛异常
        var graphReasoner = useGraph
            ? _services.GetService<GraphReasonerAgent>()
            : null;

        Task<List<Source>> retrievalTask =
            retriever.RetrieveAsync(intent, tenant, version, topK * 2, ct);

        // ★ 局部 async 函数：把 Task<GraphContext> 统一包装成 Task<GraphContext?>。
        //   直接写三元表达式会触发 CS8619，因为 Task<T> 类型参数不协变。
        async Task<GraphContext?> ReasonSafeAsync()
        {
            if (graphReasoner is null) return null;
            return await graphReasoner.ReasonAsync(intent, tenant, ct);
        }

        var graphTask = ReasonSafeAsync();

        await Task.WhenAll(retrievalTask, graphTask);

        var sources      = await retrievalTask;
        var graphContext = await graphTask;

        // ---------- 3. 生成回答 ----------
        var generator = _services.GetRequiredService<AnswerGeneratorAgent>();
        var answer = await generator.GenerateAsync(
            question, sources, graphContext, tenant, ct);

        // ---------- 4. 会话记忆（fire-and-forget） ----------
        _ = SaveToMemoryAsync(question, intent, sources, tenant);

        // ★ 返回完整结构，让 AskController 能透出 sources
        return new AskResult(answer, sources, tenant);
    }

    /// <summary>
    /// 会话记忆写入。失败只记日志，不阻塞响应。
    /// 仅当 <see cref="IMemoryStore"/> 的实现同时是 <see cref="INebulaGraphMemoryStore"/>
    /// 时才真正写 Nebula；<c>NullMemoryStore</c>（Graph 关闭时的兜底）会跳过。
    /// </summary>
    private async Task SaveToMemoryAsync(
        string question,
        QueryIntent intent,
        List<Source> sources,
        string tenant)
    {
        try
        {
            if (_memory is INebulaGraphMemoryStore nebula)
            {
                await nebula.SaveConversationAsync(new ConversationMemory
                {
                    Question  = question,
                    Entities  = intent.Entities,
                    Tenant    = tenant,
                    Timestamp = DateTime.UtcNow,
                    Sources   = sources.Select(s => s.DocId).ToList()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save memory");
        }
    }
}