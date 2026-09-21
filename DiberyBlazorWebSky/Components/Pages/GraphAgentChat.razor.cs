using DiberyBlazorWebSky.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DiberyBlazorWebSky.Components.Pages;

public partial class GraphAgentChat : ComponentBase, IDisposable
{
    [Inject] private GraphAgentApiClient GraphAgentApi { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<GraphAgentChat> Logger { get; set; } = default!;

    private readonly List<ChatMsg> _messages = new();
    private List<string> _sessions = new();
    private string _selectedSessionId = "";
    private string _userMessage = "";
    private bool _isSending;
    private bool _isLoadingSessions;
    private int _elapsedSeconds;
    private ElementReference _messagesContainer;

    private CancellationTokenSource? _sendCts;
    private Timer? _elapsedTimer;
    private DateTime _sendStartedAt;
    private bool _useStreaming = true;
    // ★ 一键复制整段对话：反馈状态
    private bool _allCopied;
    
    // ★ 复制反馈：记录最近复制的消息 ID
    private string? _lastCopiedMessageId;

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        if (_isLoadingSessions) return;
        _isLoadingSessions = true;
        StateHasChanged();

        try
        {
            var list = await GraphAgentApi.ListSessionsAsync();
            _sessions = list ?? new List<string>();
            Logger.LogInformation("加载 {Count} 个会话", _sessions.Count);
        }
        finally
        {
            _isLoadingSessions = false;
            StateHasChanged();
        }
    }

    private async Task OnSessionChangedAsync(ChangeEventArgs e)
    {
        var newId = e.Value?.ToString() ?? "";
        if (newId == _selectedSessionId) return;

        _selectedSessionId = newId;
        _messages.Clear();
        StateHasChanged();

        if (string.IsNullOrEmpty(newId)) return;

        try
        {
            var history = await GraphAgentApi.GetMessagesAsync(newId);
            foreach (var m in history)
            {
                _messages.Add(new ChatMsg
                {
                    Role = NormalizeRole(m.Role),
                    Text = m.Text,
                    Timestamp = DateTime.Now
                });
            }

            Logger.LogInformation("加载会话 {ConvId} 的 {Count} 条历史消息", newId, history.Count);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "加载历史失败");
            _messages.Add(new ChatMsg
            {
                Role = "error",
                Text = "加载历史消息失败，请重试。",
                Timestamp = DateTime.Now
            });
        }

        await ScrollToBottomAsync();
        StateHasChanged();
    }

    private static string NormalizeRole(string role) =>
        role.ToLowerInvariant() switch
        {
            "user" => "user",
            "assistant" => "assistant",
            "error" => "error",
            _ => "assistant"
        };

    private async Task ResetSessionAsync()
    {
        if (string.IsNullOrEmpty(_selectedSessionId)) return;

        var convId = _selectedSessionId;
        var ok = await GraphAgentApi.ResetSessionAsync(convId);

        if (ok)
        {
            _sessions.Remove(convId);
            _selectedSessionId = "";
            _messages.Clear();
            Logger.LogInformation("已删除会话 {ConvId}", convId);
        }
        else
        {
            _messages.Add(new ChatMsg
            {
                Role = "error",
                Text = "删除会话失败。",
                Timestamp = DateTime.Now
            });
        }

        StateHasChanged();
    }

    // ══════════════════════════════════════════════════════
    // ★ 复制消息
    // ══════════════════════════════════════════════════════

    private async Task CopyMessageAsync(ChatMsg msg)
    {
        if (string.IsNullOrEmpty(msg.Text)) return;

        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", msg.Text);

            // 显示"已复制"反馈
            _lastCopiedMessageId = msg.Id;
            StateHasChanged();

            // 2 秒后恢复
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                if (_lastCopiedMessageId == msg.Id)
                {
                    _lastCopiedMessageId = null;
                    await InvokeAsync(StateHasChanged);
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "复制消息失败（可能浏览器不支持 clipboard API）");
        }
    }

    /// <summary>
    /// 构建整段对话的 Markdown 文本。
    /// 被"复制全部"和"导出为文件"复用。
    /// </summary>
    private string BuildConversationMarkdown()
    {
        var sb = new System.Text.StringBuilder();

        // 头部元信息
        sb.AppendLine("# 图 Agent 对话");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(_selectedSessionId))
            sb.AppendLine($"- **会话 ID**: `{_selectedSessionId}`");
        sb.AppendLine($"- **导出时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- **消息数**: {_messages.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // 逐条消息
        foreach (var m in _messages)
        {
            // 角色 + Agent 名（★ 修复图标：Assistant 用 💬，GraphAssistant 用 🔧）
            var roleLabel = m.Role switch
            {
                "user" => "👤 用户",
                "assistant" => m.AgentName switch
                {
                    "Assistant" => "💬 Assistant",
                    "GraphAssistant" => "🔧 GraphAssistant",
                    _ => string.IsNullOrEmpty(m.AgentName)
                        ? "🤖 助手"
                        : $"🤖 {m.AgentName}"
                },
                "error" => "⚠️ 错误",
                _ => "❓ 未知"
            };

            sb.AppendLine($"## {roleLabel}");
            sb.AppendLine($"*{m.Timestamp:HH:mm:ss}*");
            sb.AppendLine();
            sb.AppendLine(m.Text);
            sb.AppendLine();

            if (m.ToolCallDetails.Count > 0)
            {
                sb.AppendLine("**工具调用**：");
                foreach (var detail in m.ToolCallDetails)
                {
                    var cacheTag = detail.FromCache ? " ⚡缓存" : $" {detail.ElapsedMs}ms";
                    sb.AppendLine($"- `{detail.ToolName}`{cacheTag}");
                    sb.AppendLine($"  - 参数: `{detail.Arguments}`");
                    if (!string.IsNullOrEmpty(detail.Result))
                    {
                        var resultPreview = detail.Result.Length > 300
                            ? detail.Result[..300] + "…"
                            : detail.Result;
                        sb.AppendLine($"  - 结果: {resultPreview.Replace("\n", " ")}");
                    }
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>复制整段对话到剪贴板。</summary>
    private async Task CopyAllMessagesAsync()
    {
        if (_messages.Count == 0) return;

        try
        {
            var markdown = BuildConversationMarkdown();
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", markdown);

            _allCopied = true;
            StateHasChanged();

            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                _allCopied = false;
                await InvokeAsync(StateHasChanged);
            });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "复制全部对话失败");
        }
    }

    /// <summary>导出整段对话为 .md 文件。</summary>
    private async Task ExportConversationAsync()
    {
        if (_messages.Count == 0) return;

        try
        {
            var markdown = BuildConversationMarkdown();

            // 文件名：会话ID前 8 位 + 时间戳
            var sessionTag = string.IsNullOrEmpty(_selectedSessionId)
                ? "new"
                : _selectedSessionId[..Math.Min(8, _selectedSessionId.Length)];
            var fileName = $"graph-chat-{sessionTag}-{DateTime.Now:yyyyMMdd-HHmmss}.md";

            // 复用已有的 fileDownload.js
            await Js.InvokeVoidAsync(
                "downloadTextFile", fileName, markdown, "text/markdown;charset=utf-8");

            Logger.LogInformation("导出对话成功：{FileName}", fileName);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "导出对话失败");
        }
    }
    
    // ══════════════════════════════════════════════════════
    // 发送消息
    // ══════════════════════════════════════════════════════

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(_userMessage) || _isSending) return;

        if (_useStreaming)
        {
            await SendMessageStreamAsync();
            return;
        }

        var userText = _userMessage.Trim();
        _userMessage = "";

        _messages.Add(new ChatMsg
        {
            Role = "user",
            Text = userText,
            Timestamp = DateTime.Now
        });

        _isSending = true;
        _sendStartedAt = DateTime.Now;
        _elapsedSeconds = 0;

        _elapsedTimer = new Timer(_ =>
        {
            _elapsedSeconds = (int)(DateTime.Now - _sendStartedAt).TotalSeconds;
            _ = InvokeAsync(StateHasChanged);
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        _sendCts = new CancellationTokenSource();
        await ScrollToBottomAsync();
        StateHasChanged();

        try
        {
            var response = await GraphAgentApi.SendAsync(
                userText,
                string.IsNullOrEmpty(_selectedSessionId) ? null : _selectedSessionId,
                _sendCts.Token);

            if (string.IsNullOrEmpty(_selectedSessionId) &&
                !string.IsNullOrEmpty(response.ConversationId))
            {
                _selectedSessionId = response.ConversationId;
                if (!_sessions.Contains(response.ConversationId))
                    _sessions.Add(response.ConversationId);
            }

            _messages.Add(new ChatMsg
            {
                Role = "assistant",
                Text = response.Reply,
                Timestamp = DateTime.Now,
                ToolCallDetails = response.ToolCallDetails,
                AgentName = response.AgentName
            });
        }
        catch (OperationCanceledException)
        {
            _messages.Add(new ChatMsg { Role = "error", Text = "已取消。", Timestamp = DateTime.Now });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SendMessageAsync failed");
            _messages.Add(new ChatMsg { Role = "error", Text = ex.Message, Timestamp = DateTime.Now });
        }
        finally
        {
            _elapsedTimer?.Dispose();
            _elapsedTimer = null;
            _sendCts?.Dispose();
            _sendCts = null;
            _isSending = false;
            _elapsedSeconds = 0;

            await ScrollToBottomAsync();
            StateHasChanged();
        }
    }

    private async Task SendMessageStreamAsync()
    {
        var userText = _userMessage.Trim();
        _userMessage = "";

        _messages.Add(new ChatMsg
        {
            Role = "user",
            Text = userText,
            Timestamp = DateTime.Now
        });

        var aiMsg = new ChatMsg
        {
            Role = "assistant",
            Text = "",
            Timestamp = DateTime.Now
        };
        _messages.Add(aiMsg);

        _isSending = true;
        _sendStartedAt = DateTime.Now;
        _elapsedSeconds = 0;

        _elapsedTimer = new Timer(_ =>
        {
            _elapsedSeconds = (int)(DateTime.Now - _sendStartedAt).TotalSeconds;
            _ = InvokeAsync(StateHasChanged);
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        _sendCts = new CancellationTokenSource();
        await ScrollToBottomAsync();
        StateHasChanged();

        try
        {
            await GraphAgentApi.SendStreamAsync(
                userText,
                string.IsNullOrEmpty(_selectedSessionId) ? null : _selectedSessionId,
                async delta =>
                {
                    aiMsg.Text += delta;
                    await InvokeAsync(StateHasChanged);
                    await ScrollToBottomAsync();
                },
                async reply =>
                {
                    aiMsg.Text = reply.Reply;
                    aiMsg.ToolCallDetails = reply.ToolCallDetails;
                    aiMsg.AgentName = reply.AgentName;

                    if (string.IsNullOrEmpty(_selectedSessionId) &&
                        !string.IsNullOrEmpty(reply.ConversationId))
                    {
                        _selectedSessionId = reply.ConversationId;
                        if (!_sessions.Contains(reply.ConversationId))
                            _sessions.Add(reply.ConversationId);
                    }

                    await InvokeAsync(StateHasChanged);
                },
                _sendCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (string.IsNullOrEmpty(aiMsg.Text))
                aiMsg.Text = "已取消。";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SendMessageStreamAsync failed");
            aiMsg.Text = string.IsNullOrEmpty(aiMsg.Text)
                ? $"请求失败：{ex.Message}"
                : aiMsg.Text + $"\n\n【错误】{ex.Message}";
        }
        finally
        {
            _elapsedTimer?.Dispose();
            _elapsedTimer = null;
            _sendCts?.Dispose();
            _sendCts = null;
            _isSending = false;
            _elapsedSeconds = 0;

            await ScrollToBottomAsync();
            StateHasChanged();
        }
    }

    private void CancelAsync() => _sendCts?.Cancel();

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
            await SendMessageAsync();
    }

    private async Task ScrollToBottomAsync()
    {
        try
        {
            await Task.Yield();
            await Js.InvokeVoidAsync("scrollToBottom", _messagesContainer);
        }
        catch
        {
        }
    }

    private static string GetAvatar(string role, string? agentName)
    {
        if (role == "user") return "👤";
        if (role == "error") return "⚠️";

        return agentName switch
        {
            "Assistant" => "💬",
            "GraphAssistant" => "🔧",
            _ => "🤖"
        };
    }

    public void Dispose()
    {
        _elapsedTimer?.Dispose();
        _sendCts?.Cancel();
        _sendCts?.Dispose();
    }

    private sealed class ChatMsg
    {
        // ★ 消息唯一 ID（用于复制反馈定位）
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Role { get; set; } = "assistant";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public List<GraphAgentToolCallDetail> ToolCallDetails { get; set; } = new();
        public string? AgentName { get; set; }
    }
}