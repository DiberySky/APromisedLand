理解，你的诉求是：**所有模型调用走同一个 vLLM 客户端，不要出现"这里走 MAF、那里走 IChatClient、还有个 Ollama"这种多头并存**。

先说明现状，再给统一方案。

---

## 一、现状盘点：模型调用点有哪些

| 位置 | 用的什么 | 是否 vLLM |
|---|---|---|
| `VllmChatController` | `IChatClient` | ✅ vLLM |
| `AgentFactory`（旧 `/api/chat`） | `IChatClient` | ✅ vLLM |
| `AgentSessionStore`（新 `/api/agent/chat`） | `IChatClient` | ✅ vLLM |
| `EmbeddingController` | `IEmbeddingGenerator` | ✅ vLLM embedding endpoint |
| `RerankController` | `IRerankerClient` | ✅ 独立 HTTP 服务 |
| `VllmWarmupService` | `IChatClient` | ✅ vLLM |
| `OllamaWarmupService` | `IChatClient` | ⚠️ **死代码，未注册** |

**结论**：路径其实已经全部走 vLLM 了。剩下要做的是**清理历史遗留 + 抽出单一注册点**，让"统一"成为显式约定，而不是靠"看代码发现都指向同一个 endpoint"。

---

## 二、统一后的架构

```
                              ┌──────────────────────┐
                              │   appsettings.json   │
                              │   Agent:Endpoint     │ ← 唯一配置点
                              │   Agent:ChatModel    │
                              │   Agent:ApiKey       │
                              └──────────┬───────────┘
                                         │
                                         ▼
                    ┌────────────────────────────────────────┐
                    │  ChatClientFactory（新增，单例）        │
                    │  - 读配置                               │
                    │  - 构造 OpenAIClient → vLLM             │
                    │  - 包 ThinkStrippingChatClient          │
                    │  - 输出唯一的 IChatClient               │
                    └──────────────┬─────────────────────────┘
                                   │
              ┌────────────────────┼────────────────────┐
              ▼                    ▼                    ▼
      AgentFactory         AgentSessionStore      VllmChatController
      （MAF Agent）        （MAF Agent+工具）       （直连）
              │                    │                    │
              └────────────────────┴────────────────────┘
                                   │
                              同一个 IChatClient
                              → 同一个 vLLM endpoint
                              → 同一个 ChatModel
```

**一个配置节、一个工厂、一个 `IChatClient` 实例**。

---

## 三、要做的 4 件事

### 1. 删除 `OllamaWarmupService.cs`（死代码）

```powershell
Remove-Item D:\APromisedLand\MafSampleApi\Services\OllamaWarmupService.cs
```

### 2. 新增 `Services/ChatClientFactory.cs` —— 唯一的客户端构造入口

```csharp
using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace MafSampleApi.Services;

/// <summary>
/// 唯一的 IChatClient 构造入口。所有模型调用（MAF Agent / 直连 / warmup / tools）
/// 都从这里拿同一个实例。
/// </summary>
public static class ChatClientFactory
{
    public static IChatClient Create(
        AgentOptions options,
        ILoggerFactory loggerFactory,
        ILogger logger)
    {
        var endpoint = Environment.GetEnvironmentVariable("VLLM_HTTP")
                       ?? options.Endpoint
                       ?? "http://localhost:8000";

        logger.LogInformation(
            "IChatClient → vLLM {Endpoint}, 模型: {Model}",
            endpoint, options.ChatModel);

        var openAiOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint.TrimEnd('/') + "/v1")
        };

        IChatClient inner = new OpenAIClient(
                new ApiKeyCredential(options.ApiKey),
                openAiOptions)
            .GetChatClient(options.ChatModel)
            .AsIChatClient();

        // 唯一的装饰器 —— 剥 <think>
        return new ThinkStrippingChatClient(
            inner,
            loggerFactory.CreateLogger<ThinkStrippingChatClient>());
    }
}
```

### 3. 重写 `Program.cs` 的 IChatClient 注册（用工厂）

**替换原来的 `AddSingleton<IChatClient>` 块**：

```csharp
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    return ChatClientFactory.Create(
        opts,
        sp.GetRequiredService<ILoggerFactory>(),
        sp.GetRequiredService<ILogger<Program>>());
});
```

**IChatClient 只有一个实例**：MAF Agent、VllmChatController、Warmup 都注入它。

### 4. 保持 warmup 单一入口

删掉 `OllamaWarmupService` 后，`VllmWarmupService` 是唯一的 warmup，且它注入的 `IChatClient` 就是上面那个。

```csharp
builder.Services.AddHostedService<VllmWarmupService>();
```

---

## 四、完整 `Program.cs`（统一版）

```csharp
using MafSampleApi.Models;
using MafSampleApi.Services;
using MafSampleApi.Services.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ─── 1. 配置绑定
builder.Services
    .AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<RerankerOptions>()
    .Bind(builder.Configuration.GetSection(RerankerOptions.SectionName));

// ═══════════════════════════════════════════════════════════
// 2. 唯一的 IChatClient —— 走 vLLM（工厂构造）
// ═══════════════════════════════════════════════════════════
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    return ChatClientFactory.Create(
        opts,
        sp.GetRequiredService<ILoggerFactory>(),
        sp.GetRequiredService<ILogger<Program>>());
});

// ═══════════════════════════════════════════════════════════
// 3. 唯一的 IEmbeddingGenerator —— 走 vLLM embedding
// ═══════════════════════════════════════════════════════════
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<Program>>();

    var endpoint = Environment.GetEnvironmentVariable("VLLM_EMBEDDING_HTTP")
                   ?? opts.EmbeddingEndpoint
                   ?? "http://localhost:8001";

    logger.LogInformation(
        "IEmbeddingGenerator → vLLM {Endpoint}, 模型: {Model}",
        endpoint, opts.EmbeddingModel);

    var openAiOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(endpoint.TrimEnd('/') + "/v1")
    };

    return new OpenAIClient(
            new System.ClientModel.ApiKeyCredential(opts.ApiKey),
            openAiOptions)
        .GetEmbeddingClient(opts.EmbeddingModel)
        .AsIEmbeddingGenerator();
});

// ═══════════════════════════════════════════════════════════
// 4. vLLM 探活 HttpClient
// ═══════════════════════════════════════════════════════════
builder.Services.AddHttpClient("vllm", (sp, http) =>
{
    var opts = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    var endpoint = Environment.GetEnvironmentVariable("VLLM_HTTP")
                   ?? opts.Endpoint ?? "http://localhost:8000";
    http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(5);
});

// ═══════════════════════════════════════════════════════════
// 5. Reranker
// ═══════════════════════════════════════════════════════════
builder.Services.AddHttpClient<IRerankerClient, RerankerClient>("reranker",
    (sp, http) =>
    {
        var opts = sp.GetRequiredService<IOptions<RerankerOptions>>().Value;
        var endpoint = Environment.GetEnvironmentVariable("Reranker__Endpoint")
                       ?? opts.Endpoint ?? "http://localhost:5919";
        http.BaseAddress = new Uri(endpoint.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
    });

// ═══════════════════════════════════════════════════════════
// 6. 工具
// ═══════════════════════════════════════════════════════════
builder.Services.AddSingleton<TimeTools>();
builder.Services.AddSingleton<MathTools>();
builder.Services.AddSingleton<TemplateTools>();
builder.Services.AddSingleton<KnowledgeTools>();
builder.Services.AddSingleton<IToolRegistry, DefaultToolRegistry>();

// ═══════════════════════════════════════════════════════════
// 7. MAF 服务
// ═══════════════════════════════════════════════════════════
builder.Services.AddSingleton<IInstructionTemplateStore, InMemoryInstructionTemplateStore>();
builder.Services.AddSingleton<IAgentFactory, AgentFactory>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<AgentSessionStore>();
builder.Services.AddHostedService<VllmWarmupService>();   // 唯一 warmup

// ═══════════════════════════════════════════════════════════
// 8. ASP.NET Core
// ═══════════════════════════════════════════════════════════
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

---

## 五、统一后的调用链路（一张表看清）

| 端点 | 走什么 | 底层模型调用 |
|---|---|---|
| `/api/vllm/chat` | 直连 `IChatClient` | vLLM `/v1/chat/completions` |
| `/api/vllm/chat/stream` | 直连 `IChatClient` | 同上（stream=true） |
| `/api/vllm/chat/loop*` | 直连 `IChatClient` | 同上 |
| `/api/vllm/chat/instruct*` | 直连 `IChatClient` | 同上 |
| `/api/agent/chat` | `AgentSessionStore` → `ChatClientAgent` | 同上 |
| `/api/agent/chat/stream` | 同上 | 同上 |
| `/api/agent/chat/loop*` | 同上 | 同上 |
| `/api/chat`（旧） | `AgentFactory` + `ISessionStore` | 同上 |
| `/api/chat/stream`（旧） | 同上 | 同上 |
| `/api/embedding/*` | `IEmbeddingGenerator` | vLLM `/v1/embeddings` |
| `/api/rerank` | `IRerankerClient` | reranker 服务 |
| `VllmWarmupService` | `IChatClient` | 同 chat |

**所有路径都经过同一个 `IChatClient` 单例**。

---

## 六、为什么不用 `FunctionInvokingChatClient`

方案 C 里 MAF 的 `ChatClientAgent` **自带工具循环**。如果再叠 `FunctionInvokingChatClient`：

- Agent 内部：调一次模型 → 有 tool_call → 执行 → 再调模型
- FunctionInvoking 外层：看到同样的 tool_call → **再执行一遍**
- 副作用工具（`add_document`）会被执行两次

**所以装饰链保持**：

```
OpenAI → vLLM → ThinkStrippingChatClient → IChatClient
```

**不要插** `FunctionInvokingChatClient`。

---

## 七、验证

### 7.1 编译

```powershell
dotnet build
```

应该 0 错误 0 警告（除了已有的 nullable 提示）。

### 7.2 检查应用启动日志

```
IChatClient → vLLM http://localhost:8000, 模型: qwen3-4b-awq
IEmbeddingGenerator → vLLM http://localhost:8001, 模型: bge-m3
vLLM warmup: sending ping...
vLLM warmup done in XXXms
```

**只有一行 `IChatClient → vLLM`**，说明单例生效。

### 7.3 三条路径各打一次

```http
### 直连
POST http://localhost:5324/api/vllm/chat
{ "message": "hi" }

### MAF Agent（无工具）
POST http://localhost:5324/api/agent/chat
{ "message": "hi" }

### MAF Agent（带工具）
POST http://localhost:5324/api/agent/chat
{ "message": "现在几点？" }
```

三条都返回 200，日志里**没有第二个 endpoint 的日志**。

### 7.4 确认 Ollama 相关彻底消失

```powershell
Get-ChildItem D:\APromisedLand\MafSampleApi -Recurse -Filter "*Ollama*"
# 应无输出
Get-ChildItem D:\APromisedLand\MafSampleApi -Recurse -Filter "*.cs" |
    Select-String "Ollama" -List
# 应无输出
```

---

## 八、可选的进一步统一（按需）

### 8.1 合并两套会话存储

现在有：

- `ISessionStore` + `InMemorySessionStore` → `/api/chat` 用
- `AgentSessionStore` → `/api/agent/chat` 用

两者独立字典，`sessionId` 互不相通。如果想统一：

**方案**：让 `InMemorySessionStore` 也走 `AgentSessionStore`，或反之。

**暂时不建议**——`/api/agent/chat` 是增强版（带工具、采样参数），`/api/chat` 是简化版，各自独立反而清晰。真要合并，等旧端点确定废弃时再说。

### 8.2 Embedding endpoint 也走环境变量统一下发

现在 `AgentOptions` 有 `Endpoint` 和 `EmbeddingEndpoint` 两个字段，可以从 **一个 base URL + path 后缀**派生：

```csharp
public string VllmBaseUrl { get; set; } = "http://localhost:8000";
public string ChatPath { get; set; } = "/v1";
public string EmbeddingPath { get; set; } = "/v1-embedding";  // 或者同一个
```

vLLM 如果 chat 和 embedding 同端口，这能进一步简化配置。**看你的部署实际**——如果 vLLM 分两个端口（8000/8001），保持现状就好。

### 8.3 加一个 `/api/vllm/health/endpoint` 端点

暴露当前真实指向的 endpoint，方便排查"到底连到哪了"：

```csharp
[HttpGet("endpoint")]
public IActionResult GetEndpoint([FromServices] IOptions<AgentOptions> opts)
{
    var o = opts.Value;
    var chatEndpoint = Environment.GetEnvironmentVariable("VLLM_HTTP") ?? o.Endpoint;
    var embEndpoint  = Environment.GetEnvironmentVariable("VLLM_EMBEDDING_HTTP") ?? o.EmbeddingEndpoint;
    return Ok(new { chat = chatEndpoint, embedding = embEndpoint, model = o.ChatModel });
}
```

---

## 九、一句话总结

**统一点**：`IChatClient` 单例 + `ChatClientFactory` 构造，指向 vLLM `/v1`。

**要删的**：`OllamaWarmupService.cs`（死代码）。

**不要做的**：不要给 MAF Agent 场景叠 `FunctionInvokingChatClient`（会双重调用工具）。

**效果**：所有 chat/agent/instruct/loop 端点、warmup、工具调用都经过同一个客户端，配置改一处全部生效。

需要我把 **`/api/chat`（旧）也迁移到 `AgentSessionStore`** 吗？那样就真正只剩一套会话体系。说一声。