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
