# MassTransit → Wolverine 迁移计划（TreeGraph.Api）

## Context

TreeGraph.Api 当前是全仓库唯一使用 MassTransit 的项目（`MassTransit.RabbitMQ 8.5.7`）；QuestionService / ElasticsearchService / SearchService 三个同仓库服务均已使用 Wolverine 6.51.1。统一技术栈到 Wolverine，消除 MassTransit 9.x 许可证校验这类升级风险。

迁移范围：向量同步与图同步两条事件管道（`InodeChangedEvent` 2 个消费者、`EdgeChangedEvent` 1 个消费者）+ 7 个发布注入点 + 测试基建。消息契约 `TreeGraph.Shared/Messaging/*` 框架无关，**保持不变**。

已确认决策：发布端引入仓库自有 `IEventPublisher` 包装（生产实现委托 Wolverine `IMessageBus`，测试用 `NoopEventPublisher`）。

## 关键 API 核实结论（Wolverine 6.51，官方文档）

1. `PublishMessage<T>().ToRabbitQueue(name)` 直发队列合法；`AutoProvision()` 自动声明队列；`ListenToRabbitQueue` 默认 durable。
2. 同一消息类型多 handler：默认合并为一个逻辑 handler，串行执行；任一抛异常整条消息重试重放（现有 IMemoryCache 去重窗口 + 幂等同步器已覆盖，与 MassTransit 现状语义一致）。
3. 实例 handler 类支持构造注入；`Consume(T msg, CancellationToken ct)` 签名合规（类名 Consumer 后缀 + 方法名 Consume 自动发现）。
4. 重试：`opts.Policies.OnAnyException().RetryWithCooldown(params TimeSpan[])`，每个间隔 = 一次重试，5 个间隔恰好对齐原指数重试 5 次；用尽后进入 RabbitMQ 死信队列（默认 `wolverine-dead-letter-queue`）。
5. 不调用 `UseWolverine` 则 `IMessageBus` 不在 DI、handler 不扫描，Testing 环境跳过策略零副作用。
6. 显式发布路由是终止性的，不会本地双消费；`DurabilityMode` 显式设 `Solo`（无持久化存储，避免 leader election 语义）。

## 改动清单

### 1. 包替换 — [TreeGraph.Api.csproj](file:///d:/APromisedLand/TreeGraph.Api/TreeGraph.Api.csproj)
- 删 `MassTransit.RabbitMQ 8.5.7`；加 `WolverineFx.RabbitMQ 6.51.1` + `WolverineFx.RuntimeCompilation 6.51.1`（对齐三个兄弟服务）。

### 2. 新建 `TreeGraph.Api/MessagingSky/`（3 个文件）
- `IEventPublisher.cs`：`Task Publish<T>(T message, CancellationToken ct = default);`
- `WolverineEventPublisher.cs`：`IMessageBus` 包装（`bus.PublishAsync(message)`；若 6.51 存在 CT 重载则透传）。
- `MessagingExtensions.cs`：`UseTreeGraphMessaging(this WolverineOptions opts, IConfiguration config)`：
  - `opts.Durability.Mode = DurabilityMode.Solo`
  - `opts.UseRabbitMq(new Uri(config.GetConnectionString("RabbitMQ") ?? "amqp://localhost:5672")).AutoProvision()`（保留本地回退，不用 `UseRabbitMqUsingNamedConnection`）
  - `PublishMessage<InodeChangedEvent>().ToRabbitQueue("treegraph.inode-changed")` + `ListenToRabbitQueue("treegraph.inode-changed")`
  - `PublishMessage<EdgeChangedEvent>().ToRabbitQueue("treegraph.edge-changed")` + `ListenToRabbitQueue("treegraph.edge-changed")`
  - `opts.Policies.OnAnyException().RetryWithCooldown(1s, 5s, 15s, 30s, 1m)`
  - `opts.Services.AddSingleton<IEventPublisher, WolverineEventPublisher>()`

### 3. Handler 化 3 个 Consumer（类名/文件/去重与同步逻辑不动）
- [VectorSyncService.cs](file:///d:/APromisedLand/TreeGraph.Api/NodeEavSky/SemanticSearch/VectorSyncService.cs#L285-L327) `InodeChangedConsumer`
- [GraphSyncConsumers.cs](file:///d:/APromisedLand/TreeGraph.Api/GraphSyncSky/GraphSyncConsumers.cs) `InodeGraphSyncConsumer`、`EdgeGraphSyncConsumer`
- 模式：去 `using MassTransit;` 与 `: IConsumer<T>`；`Consume(ConsumeContext<T> context)` → `Consume(T msg, CancellationToken ct)`；`context.Message.X` → `msg.X`，`context.CancellationToken` → `ct`。

### 4. 发布侧 7 处换类型（机械替换，`.Publish(x, ct)` 调用点不变）
- `IPublishEndpoint` → `IEventPublisher`，`using MassTransit;` → `using TreeGraph.Api.MessagingSky;`
- `TreeEavSky/Services/{Unit,Space,Knowledge,Category}TreeService.cs`、`NodeEavSky/Services/InodeEavFacade.cs`、`EdgeSky/Services/EdgeInstanceService.cs`、`LineSky/Controllers/LineNodeController.cs`

### 5. 接线清理
- [SemanticSearchExtensions.cs](file:///d:/APromisedLand/TreeGraph.Api/NodeEavSky/SemanticSearch/SemanticSearchExtensions.cs#L71-L101)：删 `AddVectorSyncConsumers` 与 `using MassTransit;`
- [GraphSyncExtensions.cs](file:///d:/APromisedLand/TreeGraph.Api/GraphSyncSky/GraphSyncExtensions.cs#L45-L54)：删 `AddGraphSyncConsumers`
- [Program.cs](file:///d:/APromisedLand/TreeGraph.Api/Program.cs#L101-L103)：`if (!isTesting) builder.Host.UseWolverine(opts => opts.UseTreeGraphMessaging(builder.Configuration));`

### 6. 测试基建
- `Fixtures/NoopPublishEndpoint.cs` → 重写为 `NoopEventPublisher.cs`（IEventPublisher 空桩，约 5 行）
- [EavApiFactory.cs](file:///d:/APromisedLand/TreeGraph.Api.Tests/Fixtures/EavApiFactory.cs#L49)：`services.AddMassTransit(...)` → `services.AddSingleton<IEventPublisher, NoopEventPublisher>()`
- `CategoryTreeServiceTests` / `KnowledgeTreeServiceTests` / `UnitTreeServiceTests`：构造参数类型替换

## 验证

1. `dotnet build`（全仓库无 error CS）
2. `dotnet test TreeGraph.Api.Tests`（262 绿）、`dotnet test TreeGraph.Blazor.Shared.Tests`、`dotnet test TreeGraph.Blazor.Tests`
3. 重启 AppHost：`treegrapheavapi` Running、`/health` 200
4. 冒烟：POST `/api/Lines` + `/api/LineNode`（写路径发布 InodeChangedEvent）→ DCP 日志见 Wolverine 消费记录；RabbitMQ 管理台（:15672）确认 `treegraph.inode-changed` / `treegraph.edge-changed` / `wolverine-dead-letter-queue` 队列存在且有消费者
5. `git grep -i masstransit` 全仓库无残留

## 风险

- handler 发现依赖命名约定；若启动后无消费，`opts.DescribeHandlerMatch(typeof(InodeChangedConsumer))` 诊断
- 合并 handler 后单队列重试粒度为整条消息（两 handler 重放），由去重窗口 + 幂等同步兜底，冒烟时重点观察
