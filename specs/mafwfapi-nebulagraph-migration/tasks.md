# 迁移任务清单（mafwfapi-nebulagraph-migration）

依 spec.md 执行，顺序如下：

- [ ] T1 新建 `TreeGraph.MafWfApi/Infrastructure/NebulaGraphOptions.cs`（SectionName="NebulaFastApi"，Endpoint 默认 http://localhost:9393/，Space 默认 treegraph）
- [ ] T2 新建 `TreeGraph.MafWfApi/Infrastructure/NebulaGraphRestClient.cs`（命名 HttpClient "NebulaFastApi"，POST api/v1/graph/query，本地 NebulaQueryResult DTO，success=false 抛 InvalidOperationException）
- [ ] T3 改写 `Agents/GraphTools.cs`：删 LiteGraph 数据加载（FindGraphGuidAsync/LoadNodesAsync/LoadEdgesAsync 的 REST 实现），换两条 nGQL 整图载入；8 工具签名、TrackAsync、缓存、ResolveNode、BFS 逻辑不动；构造函数依赖 LiteGraphRestClient → NebulaGraphRestClient
- [ ] T4 删 4 文件：LiteGraphRestClient.cs / LiteGraphEndpointResolver.cs / LiteGraphOptions.cs / LiteGraphExceptionMiddleware.cs
- [ ] T5 接线清理：ServiceCollectionExtensions.cs（删 AddLiteGraph + LiteGraphSdk 单例，新增 AddNebulaGraph）；Program.cs L105 删中间件；appsettings.json 换节；appsettings.Development.json 删日志行；csproj 删 LiteGraph.Sdk 包
- [ ] T6 AppHost：AppHost.cs TreeGraph() 删 AddLiteGraph 调用及注释修正（死代码旁支不动）；TreeGraphMafWfExtension.cs 删 WireIfPresent(context.LiteGraph) 与 LiteGraph 注入块，新增 NebulaFastApi 注入块
- [ ] T7 测试：删 LiteGraphEndpointResolverTests.cs；新增 NebulaGraphRestClientTests.cs（成功 / success=false / HTTP 500 三例）
- [ ] T8 文档：HANDOVER.md（端口表、启动清单、基线、保留说明）；start-aspire.ps1 先 grep 再删 LiteGraph 引用；specs/maf-wf-api/spec.md+tasks.md 末尾各加一行迁移说明
- [ ] T9 验证：dotnet build 0 错误 → MafWfApi.Tests 全绿 → Api.Tests 322 基线 → Blazor.Shared.Tests 235+1 基线
- [ ] T10 commit + push（「完美」约定）

不执行项：PG 容器 LiteGraphDb 的 DROP（破坏性，需用户另行确认）。
