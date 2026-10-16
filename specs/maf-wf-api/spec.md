# TreeGraph.MafWfApi 规格说明书

## 1. 目标与定位

创建新项目 **TreeGraph.MafWfApi**（`d:\APromisedLand\TreeGraph.MafWfApi`），作为 MAFWorkFlowApi 的 vLLM 版**替代者**。

用户已确认的决策：
- 新系统**替代**旧系统；旧系统（MAFWorkFlowApi）**停用**，文件保留在磁盘上仅供参考，不再被 AppHost 注册。
- **不包含 Graph CRUD**（不要 GraphController / GraphAdminController / EmbeddingController / Node·EdgeAuthoringService / GraphExportService / SemanticSearch / IntentParser / Reranker / Bm25 / Ollama 健康检查）。
- 复用旧端口 **5323**。
- LLM 后端从 Ollama 切换到 vLLM 容器（Chat `qwen3-4b-awq` 宿主 5723 / Embedding `bge-m3` 宿主 5719，OpenAI 兼容端点），集成模式照抄 MafRagApi 已验证实现。
- 保留的能力：Graph Function-Calling Agent（8 工具）、Assistant 路由、MCP 工具端点、Writer-Critic 工作流、Redis 会话持久化、会话目录管理。

## 2. 侦察结论（已读文件要点）

### 2.1 MAFWorkFlowApi（移植源，d:\APromisedLand\MAFWorkFlowApi）
- `Program.cs`：管线 = LiteGraphExceptionMiddleware → (dev: DeveloperExceptionPage+MapOpenApi / else UseExceptionHandler) → McpApiKeyMiddleware → UseRouting → MapControllers → MapDefaultEndpoints → MapMcp("/mcp")。DI 在 `Infrastructure/ServiceCollectionExtensions.cs` 模块化。IChatClient 来自 `AddOllamaApiClient("chat-model").AddKeyedChatClient(...)`，再桥接 keyed→non-keyed。
- `Agents/GraphAgentService.cs`：构造函数用 `[FromKeyedServices("chat-model")] IChatClient`；含路由（LlmAgentRouter→assistant 走 AssistantAgentService）、AgentSessionStore 持久化、ToolCallContext 记录、ReplySanitizer 清理、流式 ChatStreamAsync（StreamChunk/StreamChunkType 定义在本文件尾部）。
- `Agents/AssistantAgentService.cs`：无工具、无状态 Agent，同样 keyed IChatClient。
- `Agents/LlmAgentRouter.cs`：规则拦截（ObviousNonGraphKeywords + `AgentRouter.ShouldRouteToAssistant`）→ IMemoryCache 10min → LLM 兜底（MaxOutputTokens=8, Temperature=0）。**依赖 `Agents/AgentRouter.cs`**（静态关键词表，必须一并移植，非死代码）。
- `Agents/GraphTools.cs`：8 个工具方法 + TrackAsync（缓存 5min + ToolCallContext 记录），依赖 `LiteGraphRestClient`（用 `TenantGuid` 属性 + `GetAsync` 返回 JsonElement?）。
- `Agents/McpGraphTools.cs`：`[McpServerToolType]`，把 GraphTools 包装为 8 个 MCP 工具。
- `Agents/MafAgentService.cs`：`[FromKeyedServices("chat-model")] IChatClient` + `IOptions<OllamaAgentOptions>`；含通用 Assistant 会话对话 + Writer-Critic 工作流（WorkflowBuilder(_writer).AddEdge(_writer,_critic).Build().AsAIAgent(...)）。
- `Agents/OllamaAgentOptions.cs`：SectionName="Agent"，含 ModelId/Endpoint/AssistantInstructions/WriterInstructions/CriticInstructions（新项目合并进新 AgentOptions）。
- `Agents/RedisAgentSessionStore.cs`：`AgentSessionStore` + `IConversationCatalog` 实现；key 前缀 `maf:session:` / `maf:session-name:`；TTL 24h 绝对 / 4h 滑动；保存前剔除 functionCall/functionResult/tool 消息。
- `Infrastructure/`：LiteGraphOptions（SectionName="LiteGraph"，Endpoint?/TenantGuid/ApiKey）、LiteGraphEndpointResolver（services__litegraph__http__0 → ConnectionStrings__litegraph → appsettings → localhost:8701）、LiteGraphRestClient（named HttpClient "LiteGraph"，Bearer ApiKey）、LiteGraphExceptionMiddleware（HttpRequestException 全捕获映射，网络层 502）、McpApiKeyMiddleware（只拦 /mcp，Mcp:ApiKey 空则跳过，常量时间比较）、NameValueCollectionConverter（STJ 转换器）、ServiceCollectionExtensions。
- `Controllers/GraphAgentController.cs`：/api/graph-agent/chat(+stream SSE)/sessions/sessions/{id}/messages/reset/{id}/sessions/{id}/rename。
- `Controllers/WorkflowsController.cs`：/api/Workflows/writer-critic(+stream SSE)，依赖 MafAgentService。
- `Controllers/AgentsController.cs`：/api/Agents/chat + sessions 管理，依赖 MafAgentService。
- `Models/AgentDtos.cs`：ChatRequest/ToolCallDetailDto/AgentReply/SessionsReply/SessionMessage/SessionMessagesReply/WorkflowRunRequest/WorkflowStep/WorkflowReply/WorkflowStreamEvent。
- `Models/GraphAgentDtos.cs`：GraphAgentRequest/GraphAgentReply/SessionSummary/GraphAgentSessionsReply/RenameSessionRequest。
- 排除项：AgentDtos/GraphAgentDtos 之外的 Models（AuthoringResults、Node/EdgeUpsert*、EmbeddingDtos、Graph/*）、HealthChecks/、Services/ 全部、StableGuid.cs（无人使用）。
- 注：GraphAgentController 的 `using MAFWorkFlowApi.Models.Graph;` 为无用 using，移植时删除。

### 2.2 MafRagApi（vLLM 集成模式来源，d:\APromisedLand\MafRagApi）
- `Services/VllmChatClient.cs`：internal sealed IChatClient，HttpClient 直调 `{BaseAddress}/chat/completions`（相对路径 "chat/completions"，因此 HttpClient.BaseAddress 必须以 `/v1/` 结尾的基址或直接以端点为基址配 "v1/chat/completions"——实际代码用相对 "chat/completions"，故 BaseAddress 应为 `{Endpoint}/v1/`）。请求体注入 `chat_template_kwargs.enable_thinking=false`；支持 tools / response_format（引用 `StructuredOutput.AdditionalPropertiesKey`，需一并移植 `Services/StructuredOutput.cs`）。
- `Services/ThinkStrippingChatClient.cs`：DelegatingChatClient 装饰器，剥离 `<think>...</think>`（非流式正则 + 流式跨 chunk 状态机），并给 FunctionResultContent 追加 `/no_think`。
- `Services/VllmWarmupService.cs`：BackgroundService，启动 2s 后发 `hi /no_think`（MaxOutputTokens=1）预热，失败仅 Warning。
- `Models/AgentOptions.cs`：SectionName="Agent"：Endpoint（默认 http://localhost:8000）、ApiKey（"EMPTY"）、ChatModel（"qwen3-4b-awq"）、SystemPrompt（含 /no_think）、MaxSessions=256、SessionIdleTimeout=30min、ChatBudgetSeconds=120、LoopBudgetSeconds=240、VllmChatBudgetSeconds=60、VllmLoopBudgetSeconds=180。
- `Controllers/HealthController.cs`：`/api/health` 恒 200 回显配置；`/api/health/deep` 用 named HttpClient "vllm" 调 `v1/models`，校验期望模型在列，否则 503。引用 Models 中的 HealthResponseDto。
- 端点优先级约定：`VLLM_HTTP` > `Agent:Endpoint` > `http://localhost:8000`；`VLLM_EMBEDDING_HTTP` > `Embedding:Endpoint` > `http://localhost:5719`。

### 2.3 AppHost（d:\APromisedLand\APromisedLand.AppHost）
- `Extensions/VllmExtension.cs`：常量 ServedModelName="qwen3-4b-awq"、ServedEmbeddingModelName="bge-m3"、EmbeddingDimension=1024、ChatHostPort=5723、EmbeddingHostPort=5719、HttpEndpointName="http"、EmbeddingHttpEndpointName="http-embed"。`AddVllm(context)` 注册 vllm + vllm-embed 双容器并写入 `context.Vllm / context.VllmEmbed`。
- `Extensions/MafRagApiExtension.cs`（照抄模板）：AddProject 固定端口 → WireIfPresent(Redis/LiteGraph) → WithEnvironment Agent__ChatModel 等 → `WaitFor(context.Vllm).WithEnvironment("VLLM_HTTP", chatEndpoint).WithEnvironment("Agent__Endpoint", chatEndpoint)` → LiteGraph__Endpoint 注入 → WithHttpHealthCheck("/api/health") → WithOtlpExporter()。
- `AppHost.cs`：当前仅 `Tree()`/`TreeGraph()` 激活；`MafWorkFlowService()` 整体被注释（`builder.AddMafWorkFlowApi(context)` 在其内部，已是死调用）；`MafRagApi()` 被注释且内部有重复 AddVllm。
- `AppHostResourceContext.cs`：需新增 `TreeGraphMafWfApi` 属性（类型 `IResourceBuilder<ProjectResource>?`）；旧 `MafWorkFlowApi` 属性保留。
- `Projects.TreeGraph_MafWfApi` 代码生成要求 AppHost csproj 中有指向新项目的 ProjectReference（旧 MAFWorkFlowApi 的引用保留，保证 MafWorkFlowExtension.cs 仍可编译）。

## 3. 新项目文件清单（精确）

目录：`d:\APromisedLand\TreeGraph.MafWfApi\`

### 3.1 根目录
| 文件 | 动作 | 说明 |
|---|---|---|
| `TreeGraph.MafWfApi.csproj` | 新建 | Sdk.Web，net10.0，Nullable+ImplicitUsings，NoWarn 加 `MAAI001`。包：Aspire.StackExchange.Redis.DistributedCaching 13.6.1、LiteGraph.Sdk 10.2.0、Microsoft.Agents.AI 1.25.0、Microsoft.Agents.AI.Abstractions 1.25.0、Microsoft.Agents.AI.Hosting 1.25.0-preview.261014.1、Microsoft.Agents.AI.Workflows.Generators 1.25.0(PrivateAssets=all)、Microsoft.AspNetCore.OpenApi 10.0.13、Microsoft.Extensions.Caching.StackExchangeRedis 10.0.13、ModelContextProtocol.AspNetCore 2.2.0。**不含** OllamaSharp / CommunityToolkit.Aspire.OllamaSharp。ProjectReference → `..\APromisedLand.ServiceDefaults\APromisedLand.ServiceDefaults.csproj` |
| `Program.cs` | 新建 | 见 §4.1 |
| `appsettings.json` | 新建 | 见 §4.2 |
| `appsettings.Development.json` | 复制 | 从旧项目原样复制（若含 Ollama 节则删除该节） |
| `Properties/launchSettings.json` | 复制 | 从旧项目复制，端口改 5323 |

### 3.2 Agents/（复制 + 命名空间 `MAFWorkFlowApi`→`TreeGraph.MafWfApi`）
| 文件 | 额外改动 |
|---|---|
| `AgentOptions.cs` | **新建**（替代 OllamaAgentOptions）：SectionName="Agent"；`string? Endpoint`（[Url]，默认 null）；`string ApiKey="EMPTY"`；`[Required] string ChatModel="qwen3-4b-awq"`；`string SystemPrompt`（含 /no_think）；`[Required,MinLength(10)]` AssistantInstructions / WriterInstructions / CriticInstructions（默认值照抄旧 appsettings 三条中文提示词） |
| `AgentRouter.cs` | 仅改名命名空间 |
| `AssistantAgentService.cs` | `[FromKeyedServices("chat-model")] IChatClient` → 普通 `IChatClient` |
| `GraphAgentService.cs` | 同上 keyed→普通；其余不动 |
| `GraphTools.cs` | 仅改名命名空间 |
| `IConversationCatalog.cs` | 仅改名命名空间 |
| `LlmAgentRouter.cs` | keyed→普通 IChatClient |
| `MafAgentService.cs` | keyed→普通 IChatClient；`IOptions<OllamaAgentOptions>` → `IOptions<AgentOptions>`；日志 `opt.ModelId` → `opt.ChatModel` |
| `McpGraphTools.cs` | 仅改名命名空间 |
| `RedisAgentSessionStore.cs` | 前缀 `maf:session:`→`mawf:session:`、`maf:session-name:`→`mawf:session-name:` |
| `ReplySanitizer.cs` | 仅改名命名空间 |
| `ToolCallContext.cs` | 仅改名命名空间 |

### 3.3 Infrastructure/
| 文件 | 额外改动 |
|---|---|
| `LiteGraphOptions.cs` / `LiteGraphEndpointResolver.cs` / `LiteGraphRestClient.cs` / `LiteGraphExceptionMiddleware.cs` / `McpApiKeyMiddleware.cs` / `NameValueCollectionConverter.cs` | 仅改名命名空间 |
| `ServiceCollectionExtensions.cs` | 重写，见 §4.3 |

### 3.4 Services/（从 MafRagApi 复制，命名空间 `MafRagApi`→`TreeGraph.MafWfApi`）
`VllmChatClient.cs`（internal 保持）、`ThinkStrippingChatClient.cs`、`VllmWarmupService.cs`、`StructuredOutput.cs`。内容不变。

### 3.5 Models/
- `AgentDtos.cs`、`GraphAgentDtos.cs`：复制，仅改命名空间。
- `HealthResponseDto.cs`：新建，`{ string Status; string? ChatModel; string? Endpoint; }`。

### 3.6 Controllers/
| 文件 | 说明 |
|---|---|
| `GraphAgentController.cs` | 复制，改命名空间，删无用 `using ...Models.Graph;` |
| `WorkflowsController.cs` | 复制，改命名空间 |
| `AgentsController.cs` | 复制，改命名空间 |
| `ChatController.cs` | 新建：`[Route("api/chat")]`，POST 接收 `{ message }`，用普通 `IChatClient` + AgentOptions.SystemPrompt 单轮无工具对话，返回 `{ reply, model }` |
| `HealthController.cs` | 新建：照 MafRagApi 模式，`/api/health` 恒 200；`/api/health/deep` 用 named HttpClient "vllm" 探 `v1/models`，期望模型缺失/不可达 → 503 |

## 4. 关键新写内容

### 4.1 Program.cs（结构）
1. `builder.AddServiceDefaults()`
2. `AddMemoryCache()`
3. `builder.AddRedisDistributedCache("Redis")`
4. `ConfigureHttpClientDefaults`：RemoveAllResilienceHandlers + Timeout 10min
5. vLLM 端点解析（本地静态函数）：`VLLM_HTTP` 环境变量 → `Agent:Endpoint` 配置 → `http://localhost:8000`；启动日志输出 Chat/Embedding 端点与模型名
6. `AddHttpClient("vllm", c => { c.BaseAddress = new Uri(endpoint + "/v1/"); c.Timeout = 10min; })`（供 HealthController / 也可供调试）
7. `builder.Services.AddSingleton<IChatClient>(sp => new ThinkStrippingChatClient(new VllmChatClient(httpFactory建Client, opt.ChatModel, opt.ApiKey, logger), logger))`——VllmChatClient 用独立 HttpClient（BaseAddress=`{endpoint}/v1/`）
8. `AddControllers().AddJsonOptions(+NameValueCollectionConverter)`、AddProblemDetails、AddEndpointsApiExplorer、AddOpenApi
9. `AddMafAgentServices(config)` / `AddGraphDomainServices(config)` / `AddMcpServer`（见 §4.3）
10. 管线顺序与旧项目完全一致：LiteGraphExceptionMiddleware → (dev: DeveloperExceptionPage+MapOpenApi / else UseExceptionHandler) → McpApiKeyMiddleware → UseRouting → MapControllers → MapDefaultEndpoints → MapMcp("/mcp")

### 4.2 appsettings.json
```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning", "System.Net.Http.HttpClient": "Warning", "Microsoft.Extensions.ServiceDiscovery": "Warning", "Polly": "Warning" } },
  "AllowedHosts": "*",
  "Agent": {
    "ApiKey": "EMPTY",
    "ChatModel": "qwen3-4b-awq",
    "SystemPrompt": "你是一个乐于助人的助手。回答简明扼要。/no_think",
    "AssistantInstructions": "你是一名企业内部知识助手，回答简洁、专业，使用中文。",
    "WriterInstructions": "你是一名技术文案撰写专家。根据用户给出的主题，写一段结构清晰、信息密度高的中文初稿，不超过 300 字。只输出正文。",
    "CriticInstructions": "你是一名严格的技术编辑。阅读下面这篇初稿，挑出事实性、逻辑和表达问题，并直接输出一版润色后的中文终稿。只输出终稿正文，不要解释你改了什么。"
  },
  "LiteGraph": { "//": "Endpoint 保持 null", "Endpoint": null, "TenantGuid": "00000000-0000-0000-0000-000000000000", "ApiKey": "litegraphadmin" },
  "GraphAgent": { "Enabled": true, "MaxToolIterations": 5 },
  "Mcp": { "ApiKey": "" }
}
```
注：不写 `Agent:Endpoint` 键（由 VLLM_HTTP / Agent__Endpoint 环境变量注入）。

### 4.3 ServiceCollectionExtensions.cs（重写）
- `AddLiteGraph(config)`：照抄（Configure LiteGraphOptions + HttpClient "LiteGraph" + LiteGraphRestClient）。
- `AddMafAgentServices(config)`：`AddOptions<AgentOptions>().Bind(Agent).ValidateDataAnnotations().ValidateOnStart()`；RedisAgentSessionStore 单例 + AgentSessionStore 桥接 + IConversationCatalog 桥接；MafAgentService 单例；`AddHostedService<VllmWarmupService>()`（替代 OllamaWarmupService）。
- `AddGraphDomainServices(config)`：AddLiteGraph + LiteGraphSdk 单例（照抄，LiteGraphEndpointResolver.Resolve）+ Scoped：ToolCallContext/GraphTools/GraphAgentService/AssistantAgentService/LlmAgentRouter。**删除** GraphExportService/IntentParserService/SemanticSearchService/RerankerService 注册。
- `AddMcpServer`：`AddMcpServer().WithHttpTransport().WithTools<McpGraphTools>()`。**删除** OllamaModelReadyHealthCheck 与 HealthCheckPublisherOptions（Aspire 探针走 /api/health 恒 200）。

## 5. AppHost 改动

1. **新建** `APromisedLand.AppHost/Extensions/TreeGraphMafWfExtension.cs`：
   - `private const int HttpPort = 5323;`
   - `context.TreeGraphMafWfApi = builder.AddProject<Projects.TreeGraph_MafWfApi>("TreeGraphMafWfApi").WithHttpEndpoint(port: 5323, name: "http");`
   - `WireIfPresent(context.Redis).WireIfPresent(context.LiteGraph)`
   - `WithEnvironment("Agent__ChatModel", VllmExtension.ServedModelName)`
   - `if (context.Vllm is not null)`：WaitFor + `VLLM_HTTP` + `Agent__Endpoint`（chatEndpoint = GetEndpoint(HttpEndpointName)）
   - `if (context.VllmEmbed is not null)`：WaitFor + `VLLM_EMBEDDING_HTTP` + `Embedding__Endpoint` + `Embedding__Model`=ServedEmbeddingModelName + `Embedding__Dimension`=1024
   - `if (context.LiteGraph is not null)`：`LiteGraph__Endpoint` 注入
   - `WithHttpHealthCheck("/api/health", 200, "http")` + `WithOtlpExporter()`
2. **AppHostResourceContext.cs**：新增 `public IResourceBuilder<ProjectResource>? TreeGraphMafWfApi { get; set; }`（旧 MafWorkFlowApi 属性保留）。
3. **AppHost.cs**：在 `TreeGraph()` 内追加 `builder.AddVllm(context); builder.AddLiteGraph(context); builder.AddTreeGraphMafWfApi(context);`（Redis 已存在；顺序：AddVllm/AddLiteGraph 在 AddTreeGraphMafWfApi 之前）。grep 确认无激活的 `AddMafWorkFlowApi` 调用（当前在已注释的 `MafWorkFlowService()` 内，无需删除；MafWorkFlowExtension.cs 文件保留）。
4. **APromisedLand.AppHost.csproj**：新增 `<ProjectReference Include="..\TreeGraph.MafWfApi\TreeGraph.MafWfApi.csproj" />`（生成 `Projects.TreeGraph_MafWfApi`）。执行前先读 csproj 确认旧引用与 codegen 方式。
5. **解决方案**：`dotnet sln APromisedLand.sln add TreeGraph.MafWfApi/TreeGraph.MafWfApi.csproj`。

## 6. 验证步骤

1. `dotnet build d:\APromisedLand\TreeGraph.MafWfApi -v q` → 0 错误（警告可接受但需审查）。
2. `dotnet build d:\APromisedLand\APromisedLand.AppHost --no-dependencies -o "$env:TEMP\apphost-verify"` → 0 错误（规避 Aspire 运行中文件锁）；验证后删除临时目录。
3. 若有编译错误，逐项修复后重跑。
4. 汇报：端口（5323）、端点清单（/api/chat、/api/health(+deep)、/api/graph-agent/*、/api/Agents/*、/api/Workflows/*、/mcp）、配置节清单、AppHost 接线摘要。

## 7. 明确不做

- 不迁移/不修改旧 MAFWorkFlowApi 任何文件。
- 不实现 Graph CRUD、Embedding、语义搜索、Reranker。
- 不做增量数据库迁移（本项目无 DbContext）。
- 不给 vLLM 容器加 Aspire 健康检查（维持 VllmExtension 既有哲学）。
