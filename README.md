# APromisedLand — TreeGraph EAV 系统

基于 .NET 10 + Aspire + PostgreSQL 的实体-属性-值（EAV）元数据系统，支持 iNode 聚合模型。

## 项目结构

```
TreeGraph.Api/              # EAV API（实体类型/属性/单位/选项集/iNode）
TreeGraph.Blazor/           # Blazor Server 前端（MudBlazor 9.9）
TreeGraph.Shared/           # 共享 DTO
TreeGraph.Api.Tests/        # API 集成测试（Testcontainers.PostgreSql）
TreeGraph.Blazor.Tests/     # Blazor 单元测试（bUnit）
TreeGraph.Blazor.E2E.Tests/ # Playwright E2E 测试
APromisedLand.AppHost/      # Aspire 编排
```

## 固定端口

| 服务 | 端口 |
|---|---|
| TreeGraph.Api (EAV) | 5773 |
| TreeGraph.Blazor | 5783 |
| PostgreSQL | 8433 |

## 核心模型

### EAV 元数据

- **EntityType**：实体类型（`et_` + 12 位 hex 自动生成）
- **AttributeCatalog**：属性定义（`attr_` + 12 位 hex）
- **OptionSet / OptionItem**：选项集
- **Unit / UnitCategory**：单位

### iNode 聚合层（外挂表，零破坏 EAV）

- `inode_entitytype`：iNode 声明的实体类型
- `inode_entity`：iNode 下的实体实例
- `GET /api/inode/{id}/json`：iNode 全景 JSON（支持 `includeNull` / `displayName` / `units=default|base|original`）

## 测试

```
API 集成:   87
Blazor 单元: 81
E2E:        21
─────────────
总计:       189
```

### E2E 关键决策

| 决策 | 原因 | 实现 |
|---|---|---|
| **串行执行** | E2E 用真实浏览器 + SignalR，并行导致连接竞争/服务负载/DB 污染 | `xunit.runner.json` + `parallelizeTestCollections: false`（需 `CopyToOutputDirectory`） |
| **数据自清理** | 测试累积导致 DB 膨胀，触发 N+1 性能问题 | 每个建数据的测试在 `finally` 调 `TryDeleteXxxByNameAsync` |
| **Drawer 定位** | MudBlazor Temporary Drawer 关闭后保留 DOM | `BlazorHelpers.FindDrawerByTitle` 按标题文本锚定 |

详见 [BlazorHelpers.README.md](TreeGraph.Blazor.E2E.Tests/Fixtures/BlazorHelpers.README.md)。

## 性能修复记录

### EntityTypes N+1（HTTP 146 → 1）

**问题**：`EntityTypes.razor.LoadAsync` 对每个类型串行调 `GetEntityTypeAsync`，145 个类型时 146 次 HTTP，耗时 12-20s，E2E 超时。

**修复**：
- 后端：`GET /api/eav/entity-types/details`（1 次 HTTP + GroupBy 聚合属性计数）
- 前端：`LoadAsync` 改单次调用

**效果**：页面加载 < 500ms。

## 开发约定

- `.razor` 文件用 LF 行尾
- EAV 实体 ID 为 GUID 字符串（`gen_random_uuid()::text` + `HasSentinel("")`）
- 软删除用 `IsDeleted` 字段，API 层过滤
- API 响应格式由 vLLM `response_format` 校验为主，`JsonSchemaValidator` 为后备
