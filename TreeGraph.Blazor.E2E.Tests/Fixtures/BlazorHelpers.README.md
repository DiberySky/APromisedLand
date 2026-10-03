# Blazor Server E2E 辅助类说明

## 为什么需要重试？

Blazor Server 通过 **SignalR 长连接**驱动 UI。这与传统 Web 表单提交有本质差异：

| 对比项 | 传统 Web | Blazor Server |
|---|---|---|
| 事件传递 | HTTP POST | SignalR 消息 |
| 断线影响 | 无状态，POST 到达即成功 | **连接断开 → 消息丢失** |
| Playwright 视角 | Click = 事件已送达 | **Click = DOM 事件已触发（不代表服务端收到）** |

**结果**：断线期间 Playwright 报告"点击成功"，但服务端从未收到 → **静默失败**。

## 断线的常见触发时机

1. **页面导航**：旧 Circuit 释放、新 Circuit 建立（每次 `Nav.NavigateTo` 都有窗口）
2. **Aspire 服务热重载**：源码变更触发热重载，Connection 被重置
3. **SignalR 心跳超时**：默认 30 秒无活动会断开（CI 环境更敏感）
4. **首次连接建立慢**：Aspire 冷启动时，Blazor 前端还没建立连接就被点击

## 重试机制

`ClickAndNavigateAsync` 的重试策略：

```
for attempt in 1..4:
    WaitForSignalRConnectedAsync()   // 等待连接恢复
    navTask = WaitForURL(pattern)    // 注册导航等待（先于点击！）
    ClickAsync(button)               // 点击
    await navTask                     // 等导航完成
    WaitForBlazorAsync()             // 等新页面就绪
    return                           // 成功

    // 失败：延迟 500ms 后重试
```

- **导航等待先注册**：Blazor Server 软导航（pushState）可能在点击后立刻完成，后注册的 `WaitForURL` 会错过事件
- **单次尝试超时** 6s，**总上限** ≈ 24s
- **成功即退出**，不浪费尝试
- 失败时抛出的异常**携带当前 URL + 最后错误**，便于定位

## 使用规范

| 场景 | 用什么 |
|---|---|
| 点击触发导航的按钮 | `ClickAndNavigateAsync` |
| 点击不触发导航的按钮（如"保存"） | `ClickAsync`（内部仍有连接检查） |
| 操作前确认连接状态 | `WaitForSignalRConnectedAsync`（等待） |
| 测试断言连接状态 | `IsSignalRConnectedAsync`（不等待） |

## 与 Snackbar 的配合

Snackbar 默认 4 秒自动消失，但**同一时刻可能多条共存**（如"声明成功"未消失就"保存成功"）。

规则：
- **断言时**：`WaitForSnackbarAsync(page, "期望文本")` —— 传文本避免抓到旧的
- **下一步前**：`WaitForSnackbarGoneAsync(page)` —— 显式等所有 Snackbar 消失

## 常见误用

| 误用 | 后果 | 正解 |
|---|---|---|
| `ClickAndNavigateAsync` 用于非导航按钮 | 导航等待超时 | 改用 `ClickAsync` |
| `WaitForSnackbarAsync(page)` 不传文本 | 抓到旧 Snackbar | 传 `"保存成功"` 等期望文本 |
| 不调用 `WaitForSnackbarGoneAsync` 就下一步 | 时序偶发失败 | 每步操作后调用 |
| 依赖 `Task.Delay` 做等待 | 慢且不稳定 | 用 `WaitFor*` 显式等待 |

## 串行化决策（xunit.runner.json）

### 为什么 E2E 必须串行

xUnit 默认**并行跑不同测试类**（`parallelizeTestCollections: true`）。对单元测试这是好事，但对 E2E 是风险源：

| 并行的代价 | 说明 |
|---|---|
| **SignalR 连接竞争** | 多个测试同时操作浏览器 → 心跳/重连互相干扰 |
| **Aspire 服务负载** | 并发 HTTP 请求拖慢 API，放大时序抖动 |
| **DB 状态污染** | 测试间共享同一个 PostgreSQL，并行写可能互相影响 |
| **收益极小** | 21 个测试并行只省 ~30 秒 |

### 配置

`TreeGraph.Blazor.E2E.Tests/xunit.runner.json`：

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeTestCollections": false
}
```

**关键**：在 csproj 里加 `CopyToOutputDirectory=PreserveNewest`，否则 runner.json 不会复制到 `bin/`，并行设置**静默不生效**（无警告）。

### 耗时对比

| 模式 | 21 个测试总耗时 |
|---|---|
| 并行 | ~4.2 分钟 |
| 串行 | ~4.0 分钟 |

串行几乎不慢，但稳定性显著提升。

## UI 层 N+1 查询（性能陷阱）

### 现象

`EntityTypes.razor.LoadAsync` 原实现：

```csharp
foreach (var s in summaries)
{
    var detail = await Api.GetEntityTypeAsync(s.EntityTypeId);  // N 次 HTTP
}
```

DB 累积到 145 个类型后，单次 `LoadAsync` 发 **146 次串行 HTTP**，耗时 12-20s，导致 E2E 表格断言 15s 超时（2/3 复现）。

### 修复

| 层 | 改动 |
|---|---|
| 后端 | 新增 `GET /api/eav/entity-types/details`（1 次 HTTP + GroupBy 聚合属性计数） |
| 前端 | `LoadAsync` 改调 `Api.ListEntityTypeDetailsAsync()` |

### 修复效果

| 指标 | 修复前 | 修复后 |
|---|---|---|
| `LoadAsync` HTTP 次数 | 146 | **1** |
| 测试耗时 | 20-24s | **9-10s** |
| 复现率 | 2/3 失败 | **3/3 通过** |

## 测试数据自清理

E2E 每次建唯一类型（`E2E类型_xxxxxxxx`），若不清理 DB 会无限膨胀。

规则：**每个建类型的测试，`finally` 里清理**。

```csharp
using var http = new HttpClient { BaseAddress = new Uri(Fixture.Settings.ApiBaseUrl) };
try
{
    // ... UI 操作 ...
}
finally
{
    await MetadataHelpers.TryDeleteEntityTypeByNameAsync(http, displayName);
}
```

- API 建的类型：用 `TryDeleteEntityTypeAsync(http, entityTypeId)`（有 ID）
- UI 建的类型：用 `TryDeleteEntityTypeByNameAsync(http, displayName)`（反查删除）

## MudDrawer 定位陷阱

MudBlazor 9.9 `Temporary` 抽屉关闭后**保留 DOM**（`transform` 移出屏幕，无 `display:none`），页面上可能有多个 `.mud-drawer`。

| 错误做法 | 正确做法 |
|---|---|
| `Page.Locator(".mud-drawer").First` | `BlazorHelpers.FindDrawerByTitle(Page, "标题文本")` |
| `WaitForAsync(Hidden)` 等关闭 | 查 class 含 `mud-drawer--closed`（双横线） |
| `document.querySelector('.mud-drawer pre')` | `querySelectorAll` + `some()` |

同理适用于 Dialog / Popover（关闭后保留 DOM）。
