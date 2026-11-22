# TreeGraph 项目 AI 功能分析报告

> 范围：`d:\APromisedLand` 下全部 `TreeGraph*` 项目（含 `TreeGraph.Api`、`TreeGraph.MafWfApi`、`TreeGraph.SyncServices`、`TreeGraph.Blazor.Shared`、`TreeGraph.Shared` 及 Aspire 编排）。
> 依据：仅基于当前仓库代码与配置的静态分析，不含运行期实测数据。
> 日期：2026-11-22

---

## 1. 结论摘要

1. AI 能力集中在两个服务：**TreeGraph.MafWfApi**（LLM 对话 / Agent / 工作流 / MCP / 命名生成）与 **TreeGraph.SyncServices**（向量化、语义检索、向量对账）。
2. 模型统一由 **vLLM** 提供，两个 OpenAI 兼容容器：Chat `Qwen/Qwen3-4B-AWQ`（服务名 `qwen3-4b-awq`，端口 5723）、Embedding `BAAI/bge-m3`（服务名 `bge-m3`，1024 维，端口 5719）。
3. Graph Agent 通过 **Function Calling** 直接查询 NebulaGraph（经 TreeGraph.NebulaFastApi 网关），共 9 个 AI 工具；其中 8 个同时以 **MCP 工具** 形式对外暴露（`/mcp`）。
4. 语义搜索是**节点/属性/表格行**级向量检索（Qdrant collection `tree_nodes`），不是文档级 RAG；边（Edge）不生成向量。
5. 向量同步由 RabbitMQ/Wolverine 事件驱动，采用「先删后写 + 文本哈希 + 模板版本」保证幂等。
6. 前端入口在 TreeGraph.Blazor.Shared 的 `MafWfSky`：`/maf-wf/graph-agent`、`/maf-wf/writer-critic`、`/search`。

---

## 2. 架构总览

```text
TreeGraph.Blazor (5783)
  └─ MafWfSky 页面 ──HTTP/SSE──> TreeGraph.MafWfApi (5323)
                                    ├─ IChatClient ──> vLLM Chat (5723)      Qwen3-4B-AWQ
                                    ├─ Redis        会话/缓存
                                    ├─ NebulaFastApi (9393) ──> NebulaGraph     图查询工具
                                    ├─ TreeGraph.Api (5773)  ──> /api/inode/{host}/{id}/subtree
                                    └─ TreeGraph.SyncServices (5775) ──> /api/search/semantic
                                                                          │
PostgreSQL (TreeGraphDb) ──Wolverine/RabbitMQ──> TreeGraph.SyncServices ──┼─> Qdrant (tree_nodes)
                                                                          └─> vLLM Embedding (5719) bge-m3
```

---

## 3. AI 功能矩阵

| # | 功能 | 入口 | 主要实现 | 说明 |
|---|---|---|---|---|
| 1 | 无工具单轮对话 | `POST /api/chat` | [ChatController.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Controllers/ChatController.cs) | 直连 IChatClient，不建会话、不调工具 |
| 2 | 通用助手多轮对话 | `POST /api/Agents/chat` | [MafAgentService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/MafAgentService.cs) | MAF `AIAgent` + Redis `AgentSessionStore` |
| 3 | Writer-Critic 工作流 | `POST /api/Workflows/writer-critic`、`/stream` | [MafAgentService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/MafAgentService.cs#L161-L198)、[WorkflowsController.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Controllers/WorkflowsController.cs) | Sequential Workflow：Writer → Critic，终稿由 Critic 产出 |
| 4 | Graph Agent 对话 | `POST /api/graph-agent/chat`、`/chat/stream` | [GraphAgentService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/GraphAgentService.cs) | Function Calling，9 个工具；SSE 推送 Start/Delta/SearchResults/Done |
| 5 | 节点展开综合 | `POST /api/graph-agent/expand-node`；流式前缀 `__expand_node__:` | [NodeExpandService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/NodeExpandService.cs) | 子树 + EAV → 文本 → LLM 小结，写入会话历史 |
| 6 | 语义搜索 | `POST /api/search/semantic` | [SearchService.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/SearchService.cs) | 向量检索 + 阈值过滤 + 名称词法兜底 + 空间过滤 + 属性过滤 |
| 7 | 语义搜索工具 | Graph Agent 工具 `SemanticSearchNodes` | [GraphAgentService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/GraphAgentService.cs#L390-L418) | 远程调用 SyncServices；命中结果以 `SearchResults` chunk 下发卡片 |
| 8 | 向量对账 | `POST /api/search/reconcile`、`/reconcile/stream`；后台周期 6h | [SearchController.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/SearchController.cs)、[VectorReconciliationService](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/VectorReconciliationService.cs) | 修复 PG 与 Qdrant 的缺失/孤儿向量 |
| 9 | MCP 服务 | `POST /mcp` | [McpGraphTools.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/McpGraphTools.cs) | 8 个图查询工具，供外部 AI 客户端使用 |
| 10 | 中英命名生成 | `POST /api/naming/generate` | [NamingService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Services/NamingService.cs) | LLM 只做翻译，命名规则由代码确定性生成 snake_case / PascalCase |
| 11 | 健康探活 | `GET /api/health`、`/api/health/deep` | [HealthController.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Controllers/HealthController.cs) | deep 探测 vLLM `/v1/models` 是否含目标模型 |

---

## 4. 模型与推理层

### 4.1 vLLM 容器

| 项 | Chat | Embedding |
|---|---|---|
| 模型 | `Qwen/Qwen3-4B-AWQ` | `BAAI/bge-m3` |
| served-model-name | `qwen3-4b-awq` | `bge-m3` |
| 宿主端口 | 5723 | 5719 |
| 维度/上下文 | 上下文 4096 | 向量 1024 |
| 显存预算 | 0.60 | 0.30 |

- 单一数据源：[VllmExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/VllmExtension.cs)；镜像 tag 默认 `qwen3-tf451`。
- 硬件约束（代码注释明确记录）：GTX 1660 Ti / Turing / SM 7.5 / 6GB；必须 `VLLM_USE_V1=0`、`--dtype float16`、AWQ 量化、vLLM < 0.11。
- 启动策略：Embed 注册 `/health` 探针；Chat 不注册探针，通过 `WaitFor(vllmEmbed)` 串行启动，避免双容器抢显存。
- 就绪策略：[VllmWarmupService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Services/VllmWarmupService.cs) 启动后预热，总预算 6 分钟，15s→30s→45s→60s 退避重试；失败非致命。

### 4.2 自研 IChatClient

[VllmChatClient.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Services/VllmChatClient.cs) 直接调用 `/v1/chat/completions`：

- 请求体固定注入 `chat_template_kwargs.enable_thinking = false`；
- 支持非流式与 SSE 流式，解析 `content`、`tool_calls`、`finish_reason`；
- 支持 `tools` 与原生结构化输出 `response_format`（json_schema / json_object）；
- 外层再包 [ThinkStrippingChatClient.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Services/ThinkStrippingChatClient.cs) 过滤思考片段。

---

## 5. Graph Agent 与工具链

### 5.1 意图路由

[LlmAgentRouter.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/LlmAgentRouter.cs) 四级策略：

1. 现实问题词库直接判 `assistant`（天气/时间/新闻/计算/常识/生活等）；
2. 元问题关键词直接判 `assistant`（[AgentRouter.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/AgentRouter.cs)）；
3. 内存缓存命中（10 分钟）；
4. LLM 兜底分类（`MaxOutputTokens=8`、`Temperature=0`），失败回退关键词。

`forceGraph=true` 可跳过路由。

### 5.2 工具清单（GraphAgentService.BuildTools）

| 工具 | 作用 |
|---|---|
| `GetNeighbors` | 节点出边+入边邻居 |
| `GetRelations` | 两节点直接关系 |
| `ListAllNodes` | 全部节点与边 |
| `GetGraphSize` | 节点数+边数 |
| `FindPath` | 最短路径（内存 BFS） |
| `SearchNodes` | 名称关键词部分匹配 |
| `GetSubgraph` | N 跳子图（钳制 1–3） |
| `GetEdgesOfNode` | 出边/入边区分方向 |
| `SemanticSearchNodes` | 自然语言语义检索树节点（远程 SyncServices） |

工具实现位于 [GraphTools.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/GraphTools.cs)：

- 每次调用整图载入：`MATCH (n) ... LIMIT 10000`、`MATCH (a)-[e]->(b) ... LIMIT 10000`；
- 结果缓存 5 分钟（MemoryCache），键 = 工具名 + 参数；
- 节点名宽松解析：精确 → 去空格 → 去/加常见前缀 → 最长包含匹配；
- 单空间模式：`graphName` 仅为兼容调用习惯，不参与数据过滤（注释明确）。

### 5.3 系统提示约束

`BuildInstructions()` 强制要求：先调工具再回答、禁止凭记忆编造节点、未找到即如实说明、不输出工具原始格式。

### 5.4 会话与记忆

- Redis 分布式缓存：`AddRedisDistributedCache("Redis")`（[Program.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Program.cs#L24)）。
- [RedisAgentSessionStore.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/RedisAgentSessionStore.cs) 同时实现 `AgentSessionStore` 与 `IConversationCatalog`（会话列表/消息/显示名）。
- 会话键：`AgentSessionStoreKey(conversationId)`；未传 ID 时生成 GUID `N` 格式。

### 5.5 MCP

- `builder.Services.AddMcpServer().WithHttpTransport().WithTools<McpGraphTools>()`，映射 `POST /mcp`；
- 中间件 [McpApiKeyMiddleware.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Infrastructure/McpApiKeyMiddleware.cs) 仅保护 `/mcp`：`Authorization: Bearer {Mcp:ApiKey}`，常量时间比较；`Mcp:ApiKey` 为空时跳过认证并打印警告。
- MCP 暴露 8 个工具，不含 `SemanticSearchNodes`。

---

## 6. 语义搜索与向量同步

### 6.1 检索链路

1. `query` → bge-m3 向量（[EmbeddingClient.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/EmbeddingClient.cs)）；
2. Qdrant 检索候选：`topK × 4`，钳制 20–200；
3. 绝对阈值：`MinScore` 默认 0.5（请求可覆盖，Qdrant 原生过滤）；
4. 相对阈值：仅保留 `score ≥ 本轮最高分 − 0.10`；
5. 名称词法兜底（仅无 `spaceId` 时）：精确 1.0 / 前缀 0.95 / 包含 0.9；
6. 节点优先过滤：节点已命中时，其后代节点上的实体命中不再单独列出；
7. PG 反查补全名称、路径、根 ID。

### 6.2 向量内容与 payload

[SourceTextBuilder.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/SourceTextBuilder.cs) 当前模板版本 `v7`：

- 节点文本：`层级路径：… > …` + `当前节点：…`（线路节点用「所属线路」）；
- 实体文本：宿主路径前缀 + `类型 = …` + `属性 = 值`；
- 自定义表行独立成点：`类型 / 表格 / 行`；
- 覆盖宿主：五棵宿主树 + 线路 + 线路节点 + 空间节点；
- 边（Edge）无向量。

Qdrant payload 键（[QdrantVectorStore.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/QdrantVectorStore.cs)）：

`point_kind`(node/entity/row)、`inode_id`、`host_type`、`space_id`、`attribute_id`/`entity_type`、`path`、`path_ids`、`depth`、`attr_{属性Id}`、`attrn_{属性Id}`。

### 6.3 同步触发与幂等

- 消费者：[VectorSyncService.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/VectorSyncService.cs) 中的 `InodeChangedConsumer`、`EntityChangedConsumer`；
- 策略：先按 `inode_id` 删除全部旧点，再重建节点点与实体点/行点；
- 幂等基础：确定性文本 + SHA256 `sourceHash` + 模板版本；
- 去重窗口 5 秒；后台对账周期默认 6 小时。

### 6.4 故障隔离

- Embedding HTTP 超时 15 秒；
- 失败后全局冷却 60 秒（静态熔断状态跨 HttpClient 实例生效）；
- Qdrant 初始化失败不阻止宿主启动，搜索降级，写路径不受影响。

### 6.5 空间节点（SpaceNode）的 AI 覆盖

- **语义检索宿主**：[SourceTextBuilder.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/SourceTextBuilder.cs#L99-L101) 识别 `space_nodes` 表；节点文本为「层级路径：{所属空间} > 当前节点：{名称}」。
- **扁平结构**：空间节点无父子层级，唯一“祖先”是所属空间；`space_id` 取 `space_nodes.space_id`，无对应空间时归 `default`。
- **宿主类型**：Qdrant payload `host_type = "space_node"`，前端契约映射为 `SpaceNode`（[SearchService.MapHostType](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/SearchService.cs#L619-L634)）。
- **名称真源**：`space_nodes` 无 name 列，名称取自 `nodes.name`；[SearchService](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/SearchService.cs#L369-L390) 通过 `nodes` join 回填名称，并反查父空间名称。
- **节点展开**：[SubtreeService](file:///d:/APromisedLand/TreeGraph.Api/NodeEavSky/Services/SubtreeService.cs#L60-L63) 对 `SpaceNode` 走“平面宿主兜底”，只加载节点自身，不展开子树，`depth/maxDepth` 对空间节点不生效。
- **图投影**：`GraphSynchronizer` 将空间节点同步进其所属空间；[SpaceNodeTagProjector](file:///d:/APromisedLand/TreeGraph.GraphSync/SpaceNode/SpaceNodeTagProjector.cs) 把组合属性投影为派生 Tag（`{EntityType}__{AttributeName}`），与系统列 `name`/`node_type`/`is_archived` 冲突时跳过该列。
- **AI 工具读取范围**：`GraphTools` 的 nGQL 只取 `id(n)`、`n.name` 和边类型，不读取派生 Tag 列，因此 Tag 投影不进入 Graph Agent 的回答内容。
- **属性过滤**：节点点 payload 不写 `attr_{id}` / `attrn_{id}`，`AttributeFilters` 仅作用于实体点与行点；空间节点只能按名称/路径语义与 `space_id` 被检索。

---

## 7. 前端（MafWfSky）

| 页面 | 路由 | 说明 |
|---|---|---|
| GraphAgentChat | `/maf-wf/graph-agent` | 流式对话、工具调用详情、语义搜索卡片、节点展开入口 |
| WriterCritic | `/maf-wf/writer-critic` | 工作流流式输出 |
| SemanticSearch | `/search` | 语义搜索、属性过滤、同步对账（NDJSON 进度） |
| NamingTextField | 组件 | 调用 `/api/naming/generate` 回填命名 |

- 客户端：[MafWfApiClient.cs](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/MafWfSky/Services/MafWfApiClient.cs)、[MafWfSseReader.cs](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/MafWfSky/Services/MafWfSseReader.cs)。
- 弹性策略：非流式禁重试、单次尝试 5 分钟、总超时 10 分钟；SSE 无限超时、仅宽熔断（[MafWfSkyServiceCollectionExtensions.cs](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/MafWfSky/Extensions/MafWfSkyServiceCollectionExtensions.cs)）。

---

## 8. 端口与服务依赖

| 服务 | 端口 | AI 相关性 |
|---|---|---|
| vLLM Chat | 5723 | 对话/工具调用模型 |
| vLLM Embedding | 5719 | 向量化 |
| TreeGraph.MafWfApi | 5323 | AI 能力主服务 |
| TreeGraph.SyncServices | 5775 | 语义检索/向量同步 |
| TreeGraph.Api | 5773 | 子树查询（供节点展开） |
| TreeGraph.NebulaFastApi | 9393 | nGQL 查询网关 |
| TreeGraph.Blazor | 5783 | AI 功能 UI |

---

## 9. 现状边界与已识别问题

以下均为代码/配置中可直接确认的事实：

1. **边无向量**：`SourceTextBuilder` 不处理 Edge；对账接口 `category` 明确拒绝 `edge`。
2. **Qdrant 仅建 `host_type` payload 索引**：`QdrantInitializer` 注释写明「space_id 隔离尚未落地」；按 `space_id` 过滤可用但无专用索引。
3. **GraphTools 整图载入**：每次工具调用最多拉取 10000 节点 + 10000 边到内存做 BFS/模糊匹配，超出上限会被截断。
4. **节点展开深度不一致**：`NodeExpandService` 非流式将 `depth` 钳制到 0–10，而 [SubtreeController.cs](file:///d:/APromisedLand/TreeGraph.Api/NodeEavSky/Controllers/SubtreeController.cs) 将 `maxDepth` 钳制到 0–5，实际生效上限为 5。
5. **`GraphAgent:Enabled`、`GraphAgent:MaxToolIterations` 仅存在于 appsettings.json**，全仓未发现读取代码。
6. **MafWfApi 未注册全局认证/授权**：`Program.cs` 无 `AddAuthentication`/`UseAuthorization`；仅 `/mcp` 在配置了 `Mcp:ApiKey` 时受 Bearer Token 保护，默认配置为空即跳过。
7. **模板版本升级需全量重算**：`SourceTextBuilder.TemplateVersion` 注释要求文案/标签/截断规则变化时升版并全量重算；实体点与行点键不含版本。
8. **单空间图模式**：GraphTools 的 `graphName` 不参与过滤，所有查询落在 `NebulaFastApi:Space`（默认 `treegraph`）。
9. **无文档级 RAG**：向量来源仅为树节点名称/路径、EAV 属性、自定义表行；文件存储中的文件内容不参与向量化。
10. **模型规模约束**：Chat 为 4B AWQ、上下文 4096，双容器共享 6GB 显存，推理能力与并发受硬件限制。
11. **空间节点不支持子树展开**：`SubtreeService` 的平面宿主分支仅返回节点自身，节点展开（含流式 `__expand_node__`）对空间节点无层级内容。
12. **空间节点无节点级属性过滤**：属性值只写入实体点/行点 payload；空间节点自身的组合属性经 Tag 投影进入 Nebula，但 Graph Agent 工具不读取这些列。

---

## 10. 关键文件索引

| 主题 | 文件 |
|---|---|
| 模型/容器编排 | [VllmExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/VllmExtension.cs)、[TreeGraphMafWfExtension.cs](file:///d:/APromisedLand/APromisedLand.AppHost/Extensions/TreeGraphMafWfExtension.cs) |
| 对话客户端 | [VllmChatClient.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Services/VllmChatClient.cs) |
| Agent / 工作流 | [MafAgentService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/MafAgentService.cs)、[GraphAgentService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/GraphAgentService.cs) |
| 工具实现 | [GraphTools.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/GraphTools.cs)、[McpGraphTools.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/McpGraphTools.cs) |
| 节点展开 | [NodeExpandService.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/NodeExpandService.cs)、[SubtreeLoader.cs](file:///d:/APromisedLand/TreeGraph.MafWfApi/Agents/SubtreeLoader.cs)、[SubtreeController.cs](file:///d:/APromisedLand/TreeGraph.Api/NodeEavSky/Controllers/SubtreeController.cs) |
| 语义检索 | [SearchService.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/SearchService.cs)、[SearchController.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/SearchController.cs) |
| 向量同步 | [SourceTextBuilder.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/SourceTextBuilder.cs)、[VectorSyncService.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/VectorSyncService.cs)、[QdrantVectorStore.cs](file:///d:/APromisedLand/TreeGraph.SyncServices/SemanticSearch/QdrantVectorStore.cs) |
| 前端 | [GraphAgentChat.razor](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/MafWfSky/Pages/GraphAgentChat.razor)、[SemanticSearch.razor](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/MafWfSky/Pages/SemanticSearch.razor)、[WriterCritic.razor](file:///d:/APromisedLand/TreeGraph.Blazor.Shared/MafWfSky/Pages/WriterCritic.razor) |
| 设计文档 | [specs/maf-wf-api/spec.md](file:///d:/APromisedLand/specs/maf-wf-api/spec.md)、[specs/mafwfapi-nebulagraph-migration](file:///d:/APromisedLand/specs/mafwfapi-nebulagraph-migration) |
