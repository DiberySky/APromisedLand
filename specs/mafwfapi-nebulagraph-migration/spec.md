# TreeGraph.MafWfApi 图数据源迁移：LiteGraph → NebulaGraph

## 背景与目标

TreeGraph 的图真源已切换为 NebulaGraph（经 TreeGraph.NebulaFastApi 网关，端口 9393）。TreeGraph.MafWfApi 的 GraphAgent 仍通过 LiteGraph REST 拉图，需改接 NebulaGraph。

**用户拍板的边界（必须严格遵守）**：

1. **保留容器编排定义** —— `LiteGraphExtension.cs` / `LiteGraphResource.cs` / `AppHostResourceContext` 的 LiteGraph 属性 / AppHost.cs 死代码旁支（`MafRagApi()`、`MafSampleApiOllama()`、`MafWorkFlowService()`）里的 `AddLiteGraph` 调用，一律不动。
2. **仅移除 TreeGraph 活跃路径的 LiteGraph 引用** —— 即 `AppHost.cs` 的 `TreeGraph()` 函数与 `TreeGraph.MafWfApi` 项目内部。
3. **GraphTools 8 个工具公开签名不变**，`TrackAsync` 调用记录、`IMemoryCache` 缓存（5 分钟 TTL）、`ResolveNode` 宽松名称解析行为全部保留；前端 `GraphAgentChat.razor` / `MafWfApiClient` / `MafWfSseReader` 零改动。
4. `McpGraphTools`（MCP 适配器）只委托 GraphTools，**不改代码**。

## 关键事实（源码已核实）

| 项 | 事实 | 出处 |
|---|---|---|
| 查询端点 | `POST api/v1/graph/query`，体 `{space, query}` | TreeGraph.NebulaFastApi/app/routers/graph.py L29 |
| 响应 | `QueryResult{success,error_code,error_msg,data:List<Dict<string,object>>,columns,row_count}` | app/models/schemas.py L72 |
| vid | 节点 GUID 字符串，space `treegraph`，FIXED_STRING(64)（反序列化后需 `Trim('\0',' ','\t')` 再 `Guid.TryParse`） | GraphSyncSky/GraphSynchronizer.cs |
| 节点 tag | space_tree / unit_tree / category_tree / knowledge_tree / line_node / line，**全部含 `name` 属性** | GraphSyncSky/GraphSyncOptions.cs L49-62 |
| 边类型 | parent_of / line_contains / connects_to / 动态边；边显示名统一取 `type(e)` | 同上 |
| 端点注入模式 | AppHost 写 env `NebulaFastApi__Endpoint` + `WaitFor`，照抄 EavApi | TreeGraphEavApiExtension.cs L86-94 |
| MafWfApi LiteGraph 触点 | 恰好 10 个文件，无其他隐藏引用 | grep 实测 |
| LiteGraphSdk 单例 | 仅 DI 注册，无任何业务消费方 | grep `LiteGraph.Sdk` 实测 |

## 设计决策

### D1. graphName 参数语义

NebulaGraph 只有一个 space（`treegraph`），无"多图"概念，schema 也无图归属属性。决策：**8 个工具的 `graphName` 形参保留**（LLM 与前端调用习惯不变），**不再参与数据过滤**；仅在输出文案中回显。`graphName` 为空白时返回"未找到图"（保留旧空判分支）。这是数据源切换带来的语义收窄，属预期行为。

### D2. 数据加载模型：整图载入 + 内存计算（不改算法）

`LoadGraphAsync` 由 LiteGraph 三次 REST（graphs/nodes/edges）替换为两条 nGQL：

```sql
-- 节点
MATCH (n) RETURN id(n) AS vid, n.name AS name LIMIT 10000
-- 边
MATCH (a)-[e]->(b) RETURN id(a) AS src, id(b) AS dst, type(e) AS etype LIMIT 10000
```

之后 8 个工具的 BFS、邻接表、宽松解析等逻辑**逐行不动**。nGQL 中不拼接任何用户输入（graphName 不入查询；节点名只在内存做匹配），无注入面。

### D3. 新建设施（2 个新文件）

- `TreeGraph.MafWfApi/Infrastructure/NebulaGraphOptions.cs`：`SectionName = "NebulaFastApi"`（使 AppHost 注入的 `NebulaFastApi__Endpoint` 自动绑定）；`Endpoint` 默认 `http://localhost:9393/`；`Space` 默认 `treegraph`。
- `TreeGraph.MafWfApi/Infrastructure/NebulaGraphRestClient.cs`：命名 HttpClient `"NebulaFastApi"`；`QueryAsync(ngql, ct)` POST `api/v1/graph/query` 体 `{space, query}`；反序列化为本地 `NebulaQueryResult` DTO；`success=false` 抛 `InvalidOperationException`，HTTP 非 2xx 抛 `HttpRequestException`。

### D4. 删除清单（MafWfApi 内，5 个文件 + 4 处接线）

| 删除项 | 位置 |
|---|---|
| LiteGraphRestClient.cs / LiteGraphEndpointResolver.cs / LiteGraphOptions.cs / LiteGraphExceptionMiddleware.cs | TreeGraph.MafWfApi/Infrastructure/ |
| `AddLiteGraph()` 方法 + `LiteGraphSdk` 单例注册（L53-59） | Infrastructure/ServiceCollectionExtensions.cs |
| `UseMiddleware<LiteGraphExceptionMiddleware>()`（L105） | Program.cs |
| `LiteGraph` 配置节 | appsettings.json |
| resolver 日志行 | appsettings.Development.json L10 |
| `LiteGraph.Sdk` 包引用（L12） | TreeGraph.MafWfApi.csproj |
| LiteGraphEndpointResolverTests.cs | TreeGraph.MafWfApi.Tests/Infrastructure/ |

`appsettings.json` 原 `"LiteGraph"` 节替换为 `"NebulaFastApi": { "Endpoint": null, "Space": "treegraph" }`（Endpoint 留 null，与 LiteGraph 旧约定一致：由 AppHost 注入，本地回退 localhost:9393）。

异常映射中间件删除后，NebulaFastApi 调用失败由既有 `ProblemDetails`/DeveloperExceptionPage 兜底（该中间件是 LiteGraph 专用，不值得泛化保留）。

### D5. AppHost 两处改动（活跃路径）

1. `AppHost.cs` `TreeGraph()`：删 L46-48 `builder.AddLiteGraph(context);` 及注释；L43 注释 `（LiteGraph/MAF 依赖）` 改 `（MAF 依赖）`。死代码旁支 L78/L100/L123 保留。
2. `TreeGraphMafWfExtension.cs`：删 L31 `.WireIfPresent(context.LiteGraph)`；删 L76-85 LiteGraph 端点注入块；**新增** NebulaFastApi 注入块（照抄 EavApi 模式）：

```csharp
if (context.TreeGraphNebulaFastApiEndpoint is not null)
{
    context.TreeGraphMafWfApi
        .WithEnvironment("NebulaFastApi__Endpoint",
            ReferenceExpression.Create($"{context.TreeGraphNebulaFastApiEndpoint}/"));
    if (context.TreeGraphNebulaFastApi is not null)
        context.TreeGraphMafWfApi.WaitFor(context.TreeGraphNebulaFastApi);
}
else
{
    Console.WriteLine("[TreeGraphMafWfApi] ⚠️ context.TreeGraphNebulaFastApiEndpoint 为 null，图查询将连接 localhost:9393");
}
```

### D6. 测试

- 删：`TreeGraph.MafWfApi.Tests/Infrastructure/LiteGraphEndpointResolverTests.cs`。
- 增：`TreeGraph.MafWfApi.Tests/Infrastructure/NebulaGraphRestClientTests.cs`，用桩 `HttpMessageHandler` + 桩 `IHttpClientFactory` 覆盖三例（正反对照，符合工程约定）：
  1. 200 + `success:true` → 返回 data 行；
  2. 200 + `success:false` → 抛 `InvalidOperationException`（含 error_msg）；
  3. HTTP 500 → 抛 `HttpRequestException`。

### D7. 文档

- `HANDOVER.md`：L17 端口表删 `LiteGraph 8701/3001`；L74 启动清单第 7 项删 LiteGraph；测试基线行更新为当前实测（API 322 / Blazor.Shared 235+1，并注明 MafWfApi.Tests 数量）；L50 附近补一句"LiteGraph 编排定义保留但活跃路径不启动"。
- `start-aspire.ps1`：grep 其中 LiteGraph 引用并同步删除（执行时先读后改）。
- `specs/maf-wf-api/spec.md` + `tasks.md`：仅末尾各加一行迁移说明。

### D8. 环境清理（可选，需用户单独确认，不在本次执行）

PG 容器内 `LiteGraphDb` 数据库的 DROP 属破坏性操作，**本次不执行**；代码迁移完成后如需清理由用户另行拍板。

## 验证

1. `dotnet build APromisedLand.sln --nologo -v q` 0 错误。
2. `dotnet test TreeGraph.MafWfApi.Tests` 全绿（删除 1 个旧测试类 + 新增 3 个用例后的新数量为准）。
3. 双基线回归：`dotnet test TreeGraph.Api.Tests`（322 基线）、`dotnet test TreeGraph.Blazor.Shared.Tests`（235 通过 + 1 跳过）不降级。
4. 全绿后 commit + push（用户「完美」约定）。

## 风险与缓解

| 风险 | 缓解 |
|---|---|
| FIXED_STRING(64) vid 带 `\0` 填充导致 Guid 解析失败 | 统一 `Trim('\0',' ','\t')` 后 `TryParse`，解析失败的行跳过 |
| `MATCH (n) RETURN n.name` 跨 tag 取属性 | 已核实全部 6 个节点 tag 均有 `name` 列；每顶点单 tag（GraphSynchronizer 写入模式） |
| 图规模超限 | 两查询各 `LIMIT 10000`，超出部分截断（与原 MaxResults 常量同量级的防护） |
| 误删旁支编排 | 改动前对 AppHost.cs / TreeGraphMafWfExtension.cs 逐 hunk 复核，死代码函数零触碰 |
