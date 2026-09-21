# 基于现有 LiteGraph 编排的书籍知识图谱 RAG 系统完整技术方案

## 一、方案定位

本方案在**完全复用你已有的 `LiteGraphExtension.cs` 编排**的基础上，构建书籍知识图谱 RAG 系统。核心原则：

- **不重复造轮子**：LiteGraph 的 REST Server、MCP Server、Web UI、Prometheus、Grafana 编排已经完整，直接复用
- **不重复调用 LLM**：LiteGraph 已配置 Ollama 集成（`LITEGRAPH_LLM_PROVIDER=ollama`），应用层的向量生成和 Chat 调用应优先复用 LiteGraph 内置能力
- **适配现有资源类型**：使用 `LiteGraphResource.ConnectionStringExpression` 获取 LiteGraph 端点，与 `MafWorkFlowExtension.WireIfPresent(context.LiteGraph)` 的链式连接保持一致


## 二、现有 LiteGraph 编排分析

### 2.1 编排资源清单

`LiteGraphExtension.cs` 编排了六个容器资源，构成完整的 LiteGraph 运行时：

| 资源 | 容器镜像 | 端口 | 角色 |
|:---|:---|:---|:---|
| **LiteGraph REST API** | `jchristn77/litegraph:v8.1.0` | 8701 | 图数据库核心服务 |
| **LiteGraph MCP** | `jchristn77/litegraph-mcp:v8.1.0` | 8702 | MCP 协议服务，供 AI Agent 集成 |
| **LiteGraph UI** | `jchristn77/litegraph-ui:v8.1.0` | 3001 | Web 仪表盘 |
| **Prometheus** | `prom/prometheus:v2.53.0` | 9090 | 指标采集 |
| **Grafana** | `grafana/grafana-oss:11.1.0` | 3000 | 可视化监控 |
| **PostgreSQL** | 复用现有 `Postgres` | 8433 | 持久化后端 |

### 2.2 关键配置解读

**PostgreSQL 后端配置**：

```csharp
.WithEnvironment("LITEGRAPH_DB_TYPE", "Postgresql")
.WithEnvironment("LITEGRAPH_DB_HOST", "postgres")       // 容器服务名
.WithEnvironment("LITEGRAPH_DB_PORT", "5432")           // 容器内部端口
.WithEnvironment("LITEGRAPH_DB_NAME", "LiteGraphDb")
.WithEnvironment("LITEGRAPH_DB_USERNAME", "postgres")
.WithEnvironment("LITEGRAPH_DB_PASSWORD",
    resourceContext.Postgres.Resource.PasswordParameter)
.WithEnvironment("LITEGRAPH_DB_SCHEMA", "litegraph")
```

注意：环境变量使用 `LITEGRAPH_DB_*` 前缀（而非 `LITEGRAPH_POSTGRESQL_*`），所有参数通过 Aspire 环境变量注入，密码从 `Postgres` 资源的 `PasswordParameter` 引用，避免了硬编码。

**Ollama LLM 集成配置**：

```csharp
.WithEnvironment("LITEGRAPH_LLM_PROVIDER", "ollama")
.WithEnvironment("LITEGRAPH_OLLAMA_BASE_URL", "http://ollama:11434")
.WithEnvironment("LITEGRAPH_OLLAMA_CHAT_MODEL", "qwen2.5:7b")
.WithEnvironment("LITEGRAPH_OLLAMA_EMBEDDING_MODEL", "bge-large")
```

这是本方案与之前方案的核心差异点：**LiteGraph 自身已经集成了 Ollama**，可以内部完成 Chat 和 Embedding，无需应用层再单独调用 Ollama。这解锁了 LiteGraph v8.1 的 **LLM chat over graph data** 能力。

**配置文件挂载**：

```csharp
.WithBindMount(
    source: Path.Combine(AppContext.BaseDirectory, "litegraph.json"),
    target: "/app/litegraph.json",
    isReadOnly: true)
```

`litegraph.json` 提供 LiteGraph 的细粒度配置（如 HNSW 索引参数、并发限制、日志级别等）。

### 2.3 依赖关系

```
Postgres ──► LiteGraphDb ──► LiteGraph ──┬──► LiteGraphMcp
                                          ├──► LiteGraphUi
                                          ├──► Prometheus ──► Grafana
                                          └──► MafWorkFlowApi
```

LiteGraph 通过 `.WaitFor(resourceContext.LiteGraphDb!)` 等待数据库就绪；MCP、UI、Prometheus 通过 `.WaitFor(resourceContext.LiteGraph!)` 等待 LiteGraph 就绪；Grafana 等待 Prometheus 就绪。这条依赖链由 Aspire 自动管理启动顺序。


## 三、LiteGraphResource 自定义资源定义

`LiteGraphExtension.cs` 引用了 `LiteGraphResource` 自定义类型，需要配套实现：

```csharp
using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost;

public sealed class LiteGraphResource(string name)
    : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";

    private EndpointReference? _httpEndpoint;

    public EndpointReference HttpEndpoint =>
        _httpEndpoint ??= new EndpointReference(this, HttpEndpointName);

    // Aspire 通过 ConnectionStringExpression 注入到依赖项目的配置
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"Endpoint=http://{HttpEndpoint.Property(EndpointProperty.Host)}:" +
            $"{HttpEndpoint.Property(EndpointProperty.Port)}");
}
```

`ConnectionStringExpression` 让 MafWorkFlowApi 可以通过 `builder.Configuration.GetConnectionString("litegraph")` 获取端点地址，而 MCP Server 和 UI 通过 `.WithEnvironment("LITEGRAPH_API_URL", ...)` 引用同一表达式。


## 四、MafWorkFlowApi 与 LiteGraph 的连接

### 4.1 现有连接链

`MafWorkFlowExtension.cs` 中已有的连接：

```csharp
context.MafWorkFlowApi
    .WireIfPresent(context.Redis)
    .WireIfPresent(context.FileMetadataDb)
    .WireIfPresent(context.HangfireDb)
    .WireIfPresent(context.Ollama)
    .WireIfPresent(context.ChatModel, waitFor: false)
    .WireIfPresent(context.Embedding, waitFor: false)
    .WireIfPresent(context.LiteGraph);  // ← 核心连接
```

`WireIfPresent(context.LiteGraph)` 将 `LiteGraphResource` 的 `ConnectionStringExpression` 注入到 MafWorkFlowApi 的配置中。

### 4.2 应用层配置

在 MafWorkFlowApi 的 `Program.cs` 中：

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();  // Aspire 服务默认值

// ─── LiteGraph 客户端注册 ───
builder.AddLiteGraphClient(options =>
{
    options.Endpoint = builder.Configuration.GetConnectionString("litegraph")
        ?? throw new InvalidOperationException("LiteGraph 连接字符串未配置");
});

var app = builder.Build();
app.MapDefaultEndpoints();  // 健康检查 /health, /alive
app.Run();
```

`AddLiteGraphClient` 是自定义扩展方法，封装 `LiteGraph.Sdk` 的客户端初始化：

```csharp
public static class LiteGraphClientExtensions
{
    public static IHostApplicationBuilder AddLiteGraphClient(
        this IHostApplicationBuilder builder,
        Action<LiteGraphClientOptions>? configure = null)
    {
        var options = new LiteGraphClientOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton<LiteGraphClient>(sp =>
        {
            var client = new LiteGraphClient(options.Endpoint);
            // 可选：初始化租户和默认图
            return client;
        });

        builder.Services.AddHealthChecks()
            .AddCheck<LiteGraphHealthCheck>("litegraph");

        return builder;
    }
}
```

### 4.3 客户端使用约定

应用层通过 `LiteGraphClient` 与 LiteGraph 交互。租户与图的初始化策略：

```csharp
public sealed class LiteGraphInitializer(LiteGraphClient client)
{
    public async Task<Guid> EnsureBookGraphAsync(
        string bookTitle, string author, CancellationToken ct = default)
    {
        // 1. 确保租户存在（使用默认租户或自建）
        var tenantGuid = Guid.Parse("00000000-0000-0000-0000-000000000000");

        // 2. 查询是否已存在同名图
        var existing = await client.Graph.Enumerate(new EnumerationRequest
        {
            TenantGUID = tenantGuid,
            Name = bookTitle,
            MaxResults = 1
        });

        if (existing.Objects.Any()) return existing.Objects.First().GUID;

        // 3. 创建图，并在 Tags 中记录嵌入模型信息
        var graph = await client.Graph.Create(new Graph
        {
            TenantGUID = tenantGuid,
            Name = bookTitle,
            Labels = new List<string> { "书籍" },
            Tags = new NameValueCollection
            {
                { "作者", author },
                { "EmbeddingModel", "bge-large" },
                { "EmbeddingDimensionality", "1024" },
                { "LLMProvider", "ollama" },
                { "ChatModel", "qwen2.5:7b" }
            }
        });

        return graph.GUID;
    }
}
```

图创建时记录嵌入模型和维度信息，与 2.2 节中 LiteGraph 的 Ollama 配置保持一致。


## 五、LLM 集成策略

### 5.1 双重 LLM 通道分析

由于 LiteGraph 已配置 Ollama，系统存在**两个 LLM 访问路径**：

| 路径 | 调用方 | 用途 | 优势 |
|:---|:---|:---|:---|
| **路径 A：LiteGraph 内置** | `LITEGRAPH_LLM_PROVIDER=ollama` | LiteGraph 内部 Chat over Graph Data、内置向量生成 | 零应用代码、LiteGraph 原生工具调用 |
| **路径 B：应用层直连** | MafWorkFlowApi → Ollama | MAF Workflow 中的 Executor 调用 | 完全控制提示词、可融合多路检索、可观测 |

### 5.2 通道选择策略

**默认使用路径 B（应用层直连）** ，理由：

- RAG Workflow 需要**自定义的 RRF 融合逻辑**，LiteGraph 内置 Chat 不支持
- 需要**跨图查询**（多本书的图谱融合），LiteGraph 内置 Chat 绑定单图
- 需要**全链路 OpenTelemetry 追踪**，应用层调用更易观测

**保留路径 A 作为降级策略**：

- 当 MAF Workflow 出现稳定性问题时，切换为 LiteGraph 内置 Chat
- 简单的单图问答场景，直接使用内置 Chat 减少代码量
- 在 Graph Tags 中标记 `LLMProvider`，运行时可通过配置切换

### 5.3 Ollama 连接复用

在 `MafWorkFlowExtension` 中已有 `.WireIfPresent(context.Ollama)` 和 `.WireIfPresent(context.ChatModel, waitFor: false)`，应用层通过 Aspire 注入的配置获取 Ollama 端点：

```csharp
// Program.cs
builder.AddOllamaSharpChatClient("ollama");
builder.AddOllamaSharpEmbeddingGenerator("ollama");

// 在 Executor 中注入
internal sealed class AnswerGeneratorExecutor(
    IChatClient chatClient,
    IOptions<LiteGraphOptions> liteGraphOptions)
    : Executor<FusedContext, BookAnswer>("AnswerGenerator")
{
    [MessageHandler]
    private async ValueTask<BookAnswer> HandleAsync(
        FusedContext message, IWorkflowContext context,
        CancellationToken ct = default)
    {
        var prompt = BuildPrompt(message, liteGraphOptions.Value.ChatModel);
        var response = await chatClient.GetResponseAsync(prompt, ct);
        return new BookAnswer(response.Text, ExtractCitations(message), new TokenUsage());
    }
}
```

即使应用层使用路径 B，也复用 `OllamaResource` 编排的模型资源（`ChatModel`、`Embedding`），保持模型版本一致性。


## 六、数据模型与图结构

### 6.1 图模型设计

复用 LiteGraph 的属性图能力，将书籍树映射为节点和边：

| 实体 | LiteGraph 类型 | 关键属性 |
|:---|:---|:---|
| **租户** | Tenant | GUID, Name |
| **书籍图** | Graph | Name=书名, Labels=[书籍], Tags={作者/ISBN/EmbeddingModel} |
| **章节节点** | Node | Name=章节标题, Labels=[节点类型], Data={Content, SortOrder}, Vectors |
| **包含关系** | Edge | Name="包含", From=父节点, To=子节点, Cost=1 |

### 6.2 向量维度一致性

因 LiteGraph 配置了固定的嵌入模型（`bge-large`，维度 1024），所有节点向量必须使用同一模型生成。在单项录入和批量导入时，统一通过 LiteGraph 内置的向量生成或应用层复用同一 `EmbeddingModelResource`：

```csharp
public sealed class VectorDimensionGuard(LiteGraphClient client)
{
    private readonly Dictionary<Guid, int> _graphDimensions = new();

    public async Task<int> GetExpectedDimensionAsync(
        Guid graphGuid, CancellationToken ct = default)
    {
        if (_graphDimensions.TryGetValue(graphGuid, out var dim)) return dim;

        var graph = await client.Graph.Retrieve(graphGuid);
        var dimStr = graph.Tags?["EmbeddingDimensionality"]?.ToString();

        if (string.IsNullOrEmpty(dimStr))
            throw new InvalidOperationException(
                $"图 {graphGuid} 未记录嵌入维度，请检查图初始化");

        dim = int.Parse(dimStr);
        _graphDimensions[graphGuid] = dim;
        return dim;
    }

    public async Task ValidateAsync(
        Guid graphGuid, float[] vector, CancellationToken ct = default)
    {
        var expected = await GetExpectedDimensionAsync(graphGuid, ct);
        if (vector.Length != expected)
            throw new InvalidOperationException(
                $"向量维度不匹配：期望 {expected}，实际 {vector.Length}");
    }
}
```

维度信息记录在图 Tags 中，搜索时自动校验，避免切换嵌入模型导致向量维度不匹配。


## 七、双通道录入（适配现有编排）

### 7.1 导入通道：MAF Workflow 批量摄取

导入工作流的 Executor 拓扑不变，但**数据库写入和向量生成复用现有 LiteGraph 编排**：

```
BatchImportRequest
      │
      ▼
┌─────────────────┐
│ FileValidator   │  格式校验、大小限制、白名单
└────────┬────────┘
         ▼
┌─────────────────┐
│ DocumentParser  │  从 SeaweedFS 读取文件并解析
└────────┬────────┘
         ▼
┌─────────────────┐
│ TextChunker     │  按章节边界分块
└────────┬────────┘
         ▼
┌─────────────────┐
│ Deduplication   │  内容哈希去重（查询 LiteGraph）
└────────┬────────┘
         │
    ┌────┴────┐  Fan-out
    ▼         ▼
┌────────┐ ┌──────────────┐
│Graph   │ │Embedding     │
│Builder │ │Generator     │
│(LiteGraph)│(LiteGraph内置)│
└───┬────┘ └──────┬───────┘
    └──────┬───────┘  Fan-in
           ▼
┌─────────────────┐
│ IndexBuilder    │  LiteGraph HNSW 索引
└────────┬────────┘
         ▼
┌─────────────────┐
│ ImportNotifier  │  Hangfire 记录 + SSE 通知
└─────────────────┘
```

**关键适配点**：

- `GraphBuilderExecutor` 通过 `LiteGraphClient` 写入节点/边，使用 `LiteGraphDb` 数据库
- `EmbeddingGeneratorExecutor` 调用 `LiteGraphClient` 的向量生成 API（内部走 Ollama），或直接调用应用层注入的 `IEmbeddingGenerator`（同一个 Ollama 模型资源）
- `DeduplicationExecutor` 通过 `LiteGraphClient` 批量查询已有节点的 ContentHash

### 7.2 单项录入通道：轻量级 Service

单项录入不依赖 MAF Workflow，直接操作 LiteGraph 事务：

```csharp
public sealed class SingleEntryService(
    LiteGraphClient client,
    VectorDimensionGuard dimensionGuard,
    ILogger<SingleEntryService> logger) : ISingleEntryService
{
    public async Task<EntryResult> CreateNodeAsync(
        SingleNodeEntry entry, CancellationToken ct = default)
    {
        // 1. 校验（不含向量生成）
        var validation = await ValidateAsync(entry, ct);
        if (!validation.IsValid) return EntryResult.Failed(validation.Errors);

        // 2. 事务内写入节点和边
        Guid nodeGuid;
        using (var transaction = await client.BeginTransactionAsync(ct))
        {
            try
            {
                var node = await client.Node.Create(new Node { /* ... */ }, transaction);
                if (entry.ParentNodeGuid.HasValue)
                    await client.Edge.Create(new Edge { /* ... */ }, transaction);
                await transaction.CommitAsync(ct);
                nodeGuid = node.GUID;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                return EntryResult.Failed(new[] { new ValidationError("_", ex.Message) });
            }
        }

        // 3. 事务外异步生成向量（可选）
        if (entry.GenerateVector)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await GenerateAndAttachVectorAsync(nodeGuid, entry, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "节点 {Guid} 向量生成失败", nodeGuid);
                    await MarkPendingVectorAsync(nodeGuid, ct);
                }
            }, ct);
        }

        return EntryResult.Success(nodeGuid);
    }
}
```

向量生成放在事务外异步执行，避免事务持有期间长时间占用数据库连接。


## 八、RAG 查询工作流

### 8.1 Workflow 拓扑

```
BookQueryRequest { GraphGuid, Question, TopK }
      │
      ▼
┌──────────────────┐
│ QueryPreprocessor│  查询重写 + 向量生成（Ollama）
└────────┬─────────┘
         │ Fan-out
    ┌────┴────┐
    ▼         ▼
┌────────┐ ┌──────────────┐
│Vector  │ │GraphTraversal│
│Search  │ │              │
│(LiteGraph)│(LiteGraph)   │
└───┬────┘ └──────┬───────┘
    └──────┬───────┘  Fan-in
           ▼
┌──────────────────┐
│ RrfFusion        │  IReadOnlyList<RetrievalResult>
└────────┬─────────┘
         ▼
┌──────────────────┐
│ AnswerGenerator  │  Ollama ChatModel
└──────────────────┘
```

### 8.2 GraphGuid 传递修正

`GraphGuid` 通过 `BookQueryRequest` 传递，而非在 Executor 构造函数注入：

```csharp
public sealed record BookQueryRequest(
    Guid GraphGuid,
    string Question,
    int TopK = 10);

public sealed record ProcessedQuery(
    Guid GraphGuid,
    string OriginalQuestion,
    string RewrittenQuery,
    float[] QueryVector,
    List<string> Keywords,
    int TopK);
```

`VectorSearchExecutor` 和 `GraphTraversalExecutor` 从消息中获取 `GraphGuid`，支持多本书查询：

```csharp
internal sealed partial class VectorSearchExecutor(LiteGraphClient client)
    : Executor<ProcessedQuery, RetrievalResult>("VectorSearch")
{
    [MessageHandler]
    private async ValueTask<RetrievalResult> HandleAsync(
        ProcessedQuery message, IWorkflowContext context,
        CancellationToken ct = default)
    {
        var searchResults = await client.Node.VectorSearch(
            graphGuid: message.GraphGuid,  // 从消息获取
            queryVector: message.QueryVector,
            topK: message.TopK);

        return new RetrievalResult(
            searchResults.Objects.Select(/* 映射 */).ToList(),
            RetrievalSource.Vector);
    }
}
```

### 8.3 不依赖 QueueStateUpdateAsync

Executor 之间通过强类型消息传递，不使用 `context.QueueStateUpdateAsync` 保存中间状态，避免冗余设计。所有必要数据（`OriginalQuestion`、`GraphGuid`、`TopK`）都在消息 record 中携带。

### 8.4 WatchStreamAsync 缺陷应对

使用 `ResilientSseStreamer` 增加心跳和超时保护（详见修订版方案 3.1 节），应对 MAF 1.13.0 的 `WatchStreamAsync` 静默停止问题。

### 8.5 Checkpoint 持久化

```csharp
builder.Services.AddSingleton<ICheckpointStorage>(
    new FileSystemJsonCheckpointStore(
        builder.Configuration["CheckpointStorage:Path"] ?? "./checkpoints"));
```

`FileSystemJsonCheckpointStore` 将检查点以 JSON 文件持久化，适用于导入工作流的断点续传场景。


## 九、Aspire AppHost 完整编排

### 9.1 现有 AppHost 的适配

`AppHost.cs` 中 `MafWorkFlowService()` 已编排全部所需资源：

```csharp
void MafWorkFlowService()
{
    builder.AddPostgres(context);        // 含 LiteGraphDb
    builder.AddRedis(context);
    builder.AddSeaweedFs(context);       // 原始文件存储
    builder.AddOllama(context);          // LLM 模型资源
    builder.AddFileStorageApi(context);
    builder.AddLiteGraph(context);       // 完整 LiteGraph 编排
    builder.AddMafWorkFlowApi(context);
    builder.AddBlazorWeb(context);
}
```

无需修改，直接使用。

### 9.2 新增：DevUI 资源（可选）

如需要可视化调试 MAF Agent 和 Workflow，在 `MafWorkFlowService()` 中增加：

```csharp
builder.AddDevUI("maf-devui")
    .WithAgentService(context.MafWorkFlowApi, agents: [
        new("book-rag-agent")
    ])
    .WaitFor(context.MafWorkFlowApi);
```

DevUI 中的 Agent 名称必须与 MafWorkFlowApi 中 `AddAIAgent("book-rag-agent", ...)` 注册的名称一致。

### 9.3 BlazorWeb 引用更新

`AppHost.cs` 中的 `builder.AddBlazorWeb(context)` 需要确保 BlazorWeb 引用 MafWorkFlowApi：

```csharp
public static void AddBlazorWeb(
    this IDistributedApplicationBuilder builder,
    AppHostResourceContext context)
{
    context.BlazorWeb = builder
        .AddProject<Projects.BookRAG_UI>("blazorweb")
        .WithReference(context.MafWorkFlowApi!)  // 服务发现
        .WaitFor(context.MafWorkFlowApi!);
}
```

前端通过服务名 `https://MAFWorkFlowApi` 访问 API，Aspire 自动解析实际地址。


## 十、MafWorkFlowApi 的 Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// ─── Aspire 服务默认值 ───
builder.AddServiceDefaults();

// ─── LiteGraph 客户端 ───
builder.AddLiteGraphClient(options =>
{
    options.Endpoint = builder.Configuration.GetConnectionString("litegraph")
        ?? throw new InvalidOperationException("LiteGraph 连接字符串未配置");
});

// ─── Ollama 客户端（复用 LiteGraph 编排的模型资源）───
builder.AddOllamaSharpChatClient("ollama");
builder.AddOllamaSharpEmbeddingGenerator("ollama");

// ─── Redis 分布式缓存 ───
builder.AddRedisDistributedCache("Redis");

// ─── 领域服务 ───
builder.Services.Configure<BookHierarchyOptions>(
    builder.Configuration.GetSection("BookHierarchy"));
builder.Services.AddScoped<IEntryValidator, EntryValidator>();
builder.Services.AddScoped<ISingleEntryService, SingleEntryService>();
builder.Services.AddScoped<VectorDimensionGuard>();
builder.Services.AddScoped<LiteGraphInitializer>();
builder.Services.AddSingleton<IBookParserFactory, BookParserFactory>();

// ─── MAF Workflow ───
builder.Services.AddSingleton<ICheckpointStorage>(
    new FileSystemJsonCheckpointStore(
        builder.Configuration["CheckpointStorage:Path"] ?? "./checkpoints"));
builder.Services.AddSingleton<IngestionWorkflowFactory>();
builder.Services.AddSingleton<RagWorkflowFactory>();

// ─── Hangfire（后台导入任务）───
builder.Services.AddHangfire(config => config
    .UsePostgreSqlStorage(builder.Configuration.GetConnectionString("HangfireDb")));
builder.Services.AddHangfireServer();

// ─── MAF Agent ───
builder.AddAIAgent("book-rag-agent", (sp, key) =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    return chatClient.AsAIAgent(
        instructions: "你是书籍知识助手，基于 LiteGraph 中的书籍内容回答问题。",
        name: key);
});

// ─── OpenTelemetry ───
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddPrometheusExporter();
        metrics.AddMeter("BookRAG.Ingestion");
        metrics.AddMeter("BookRAG.Retrieval");
        metrics.AddMeter("BookRAG.Entry");
    });

var app = builder.Build();

// ─── 端点映射 ───
app.MapImportEndpoints();
app.MapEntryEndpoints();
app.MapChatEndpoints();
app.MapPrometheusScrapingEndpoint();
app.MapDefaultEndpoints();

app.Run();
```

### 10.1 appsettings.json 配置

```json
{
  "BookHierarchy": {
    "AllowedChildren": {
      "书": ["部", "章"],
      "部": ["章"],
      "章": ["节"],
      "节": ["段落"],
      "段落": []
    }
  },
  "CheckpointStorage": {
    "Path": "./checkpoints"
  },
  "LiteGraph": {
    "DefaultTenantGuid": "00000000-0000-0000-0000-000000000000"
  }
}
```

`LiteGraph` 的端点由 Aspire 服务发现自动注入，无需在 `appsettings.json` 中配置。


## 十一、客户端 MudBlazor TreeView

### 11.1 版本要求

**必须使用 MudBlazor 9.2.0 或以上**，以获取 `ServerData` 懒加载状态重置 bug 的修复。低于此版本会在展开节点后状态异常。

### 11.2 与现有 API 的对接

BlazorWeb 通过 Aspire 服务发现访问 MafWorkFlowApi：

```csharp
// Program.cs
builder.Services.AddHttpClient<IBookGraphApiClient, BookGraphApiClient>(client =>
{
    client.BaseAddress = new Uri("https://MAFWorkFlowApi");
});
```

Aspire 的服务发现会自动将 `https://MAFWorkFlowApi` 解析为实际地址。

### 11.3 TreeView 懒加载

`BookTreeView.razor` 的 `ServerData` 回调通过 `IBookGraphApiClient` 调用 MafWorkFlowApi 的子节点接口：

```razor
<MudTreeView T="BookTreeNode"
             Items="@_rootItems"
             ServerData="@LoadServerData"
             ExpandOnClick="true"
             SelectionMode="SelectionMode.SingleSelection"
             @bind-SelectedValue="_selectedNode"
             Height="calc(100vh - 240px)"
             Dense="true"
             Comparer="@_nodeComparer">
    <ItemTemplate Context="item">
        <MudTreeViewItem Value="@item.Value"
                         Text="@item.Value.Title"
                         Icon="@GetIcon(item.Value.NodeType)"
                         ExpandButtonIcon="@(item.Value.HasChildren
                             ? Icons.Material.Filled.KeyboardArrowRight
                             : string.Empty)" />
    </ItemTemplate>
</MudTreeView>
```

`ExpandButtonIcon` 在无子节点时设置为空字符串，阻止无效的展开请求，避免对叶子节点调用 `ServerData`。


## 十二、部署与可观测性

### 12.1 开发环境

```bash
# 启动完整 Aspire 编排
dotnet run --project APromisedLand.AppHost
```

Aspire Dashboard 自动打开，可以查看：

- **资源状态**：Postgres、LiteGraph、Ollama、MafWorkFlowApi 等资源的启动顺序和健康状态
- **LiteGraph Dashboard**：`http://localhost:3001` 查看图数据
- **Prometheus**：`http://localhost:9090` 查询 LiteGraph 指标
- **Grafana**：`http://localhost:3000`（admin/admin）查看预置仪表盘
- **MafWorkFlowApi**：`http://localhost:5323` API 端点

### 12.2 生产环境

Aspire 13.5.4 支持基于管道的部署，可将相同的应用拓扑部署到：

- **Azure Container Apps**：`builder.AddAzureContainerAppEnvironment("aca")`
- **Kubernetes**：`builder.AddKubernetesEnvironment("k8s")`（Aspire 13.3+，基于 Helm）
- **Docker Compose**：`aspire publish --output-format docker-compose`

LiteGraph 容器的生产配置已由 `LiteGraphExtension.cs` 完整定义，无需额外调整。

### 12.3 可观测性链路

```
MafWorkFlowApi ──OTel──► Aspire Dashboard（开发）
                        │
                        └──► Prometheus ──► Grafana（生产）
                              ▲
                              │
LiteGraph ──/metrics──────────┘
```

LiteGraph 通过 `/metrics` 端点暴露指标，由 Aspire 编排的 Prometheus 采集，Grafana 可视化。MafWorkFlowApi 通过 OpenTelemetry 导出 MAF Workflow 的事件和指标，同时接入 Aspire Dashboard 和 Prometheus。


## 十三、分阶段实施

### 阶段一：MVP（4-6 周）

- 复用现有 LiteGraph 编排，验证 PostgreSQL 后端和 Ollama 集成
- 实现单项录入通道（EntryValidator + SingleEntryService + API）
- 实现基础 RAG 查询（顺序调用，不引入 MAF Workflow）
- MudBlazor TreeView 懒加载 + 录入表单
- **验收标准**：可以通过 UI 创建书籍树，查询能返回基于向量搜索的答案

### 阶段二：工程化（4-6 周）

- 引入 MAF Workflow 重构导入和查询管道
- Fan-out/Fan-in 并行执行
- SSE 进度推送（含 `ResilientSseStreamer`）
- Checkpoint 持久化（`FileSystemJsonCheckpointStore`）
- Hangfire 后台导入任务
- **验收标准**：批量导入中断后可恢复，SSE 进度流稳定不中断

### 阶段三：生产化（3-4 周）

- OpenTelemetry 全链路追踪
- 文件上传安全加固（大小限制、格式白名单）
- 凭证管理（Aspire Parameter 注入）
- 性能调优（HNSW 索引参数、并发控制）
- **验收标准**：通过 Aspire 部署到生产环境，监控指标正常


## 十四、回退策略

**MAF Workflow 稳定性风险**：MAF 1.0 于 2026-04 GA，社区验证时间较短。建议锁定版本 `Microsoft.Agents.AI 1.22.0`，升级前回归测试。

**天然安全网**：

| 故障场景 | 降级路径 |
|:---|:---|
| MAF Workflow 不稳定 | 单项录入通道（非 Workflow Service）独立运行 |
| 批量导入中断 | 降级为顺序处理（Dedup → GraphBuild → Embed → Index） |
| RAG Workflow 异常 | 切换为 LiteGraph 内置 Chat（路径 A） |
| LiteGraph REST 异常 | 降级为嵌入式 `LiteGraphClient`（进程内） |


## 十五、总结

本方案在**完全复用现有 `LiteGraphExtension.cs` 编排**的基础上，构建书籍知识图谱 RAG 系统：

1. **复用现有编排**：LiteGraph REST Server、MCP、UI、Prometheus、Grafana 编排无需修改，直接通过 `WireIfPresent(context.LiteGraph)` 接入 MafWorkFlowApi

2. **复用 LiteGraph 内置 Ollama 集成**：LiteGraph 已配置 `LITEGRAPH_LLM_PROVIDER=ollama`，应用层优先复用同一 Ollama 模型资源，避免重复部署

3. **双重 LLM 通道**：默认使用应用层直连（路径 B，支持 RRF 融合和跨图查询），保留 LiteGraph 内置 Chat（路径 A）作为降级策略

4. **PostgreSQL 后端**：LiteGraph 使用复用现有 Postgres 的 `LiteGraphDb` 数据库，通过 `LITEGRAPH_DB_*` 环境变量注入连接配置

5. **双通道录入**：导入通道（MAF Workflow 批量摄取）与单项录入通道（轻量级 Service 事务写入）共享领域模型和校验规则

6. **Aspire 编排一致性**：MafWorkFlowApi、BlazorWeb 通过 Aspire 服务发现自动获取 LiteGraph 和 Ollama 端点，无需硬编码

7. **分阶段落地**：MVP → 工程化 → 生产化三阶段，双通道设计作为天然安全网