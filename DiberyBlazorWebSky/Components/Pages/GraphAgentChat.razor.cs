using DiberyBlazorWebSky.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DiberyBlazorWebSky.Components.Pages;

public partial class GraphAgentChat : ComponentBase, IDisposable
{
    [Inject] private GraphAgentApiClient GraphAgentApi { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<GraphAgentChat> Logger { get; set; } = default!;

    private readonly List<ChatMsg> _messages = new();
    private List<GraphAgentSessionSummary> _sessions = new();

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

    // 复制反馈
    private string? _lastCopiedMessageId;
    private bool _allCopied;

    // 会话重命名
    private bool _showRenameDialog;
    private string _renamingSessionName = "";
    private bool _isRenamingSession;

    // 会话搜索
    private string _sessionFilter = "";

    // 图片上传
    private string? _pendingImageDataUrl;
    private string? _pendingImageName;
    private const long MaxImageSizeBytes = 2 * 1024 * 1024;

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionsAsync();
    }

    // ══════════════════════════════════════════════════════
    // 图片上传
    // ══════════════════════════════════════════════════════

    private async Task OnImageSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file == null) return;

        if (file.Size > MaxImageSizeBytes)
        {
            _messages.Add(new ChatMsg
            {
                Role = "error",
                Text = $"图片过大（{file.Size / 1024} KB），请选择小于 2 MB 的图片。",
                Timestamp = DateTime.Now
            });
            StateHasChanged();
            return;
        }

        try
        {
            using var stream = file.OpenReadStream(MaxImageSizeBytes);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            _pendingImageDataUrl = $"data:{file.ContentType};base64,{base64}";
            _pendingImageName = file.Name;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "读取图片失败");
            _messages.Add(new ChatMsg
            {
                Role = "error",
                Text = $"读取图片失败：{ex.Message}",
                Timestamp = DateTime.Now
            });
        }

        StateHasChanged();
    }

    private void ClearPendingImage()
    {
        _pendingImageDataUrl = null;
        _pendingImageName = null;
        StateHasChanged();
    }

    // ══════════════════════════════════════════════════════
    // 会话列表
    // ══════════════════════════════════════════════════════

    private async Task LoadSessionsAsync()
    {
        if (_isLoadingSessions) return;
        _isLoadingSessions = true;
        StateHasChanged();

        try
        {
            var list = await GraphAgentApi.ListSessionsAsync();
            _sessions = list ?? new List<GraphAgentSessionSummary>();
            Logger.LogInformation("加载 {Count} 个会话", _sessions.Count);
        }
        finally
        {
            _isLoadingSessions = false;
            StateHasChanged();
        }
    }

    private IEnumerable<GraphAgentSessionSummary> FilteredSessions
    {
        get
        {
            IEnumerable<GraphAgentSessionSummary> filtered = string.IsNullOrWhiteSpace(_sessionFilter)
                ? _sessions
                : _sessions.Where(s =>
                    s.Id.Contains(_sessionFilter, StringComparison.OrdinalIgnoreCase)
                    || (s.DisplayName?.Contains(_sessionFilter, StringComparison.OrdinalIgnoreCase) ?? false));

            var list = filtered.ToList();

            if (!string.IsNullOrEmpty(_selectedSessionId) &&
                !list.Any(s => s.Id == _selectedSessionId))
            {
                var current = _sessions.FirstOrDefault(s => s.Id == _selectedSessionId);
                if (current is not null)
                    list.Insert(0, current);
            }

            return list;
        }
    }

    private static string GetSessionLabel(GraphAgentSessionSummary s)
    {
        if (!string.IsNullOrEmpty(s.DisplayName))
            return s.DisplayName;

        if (string.IsNullOrEmpty(s.Id)) return "";
        return s.Id.Length > 14 ? s.Id[..12] + "…" : s.Id;
    }

    private async Task OnSessionChangedAsync(ChangeEventArgs e)
    {
        _sessionFilter = "";

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
            _sessions.RemoveAll(s => s.Id == convId);
            _selectedSessionId = "";
            _sessionFilter = "";
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
    // 复制 / 导出
    // ══════════════════════════════════════════════════════

    private async Task CopyMessageAsync(ChatMsg msg)
    {
        if (string.IsNullOrEmpty(msg.Text)) return;

        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", msg.Text);

            _lastCopiedMessageId = msg.Id;
            StateHasChanged();

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
            Logger.LogWarning(ex, "复制消息失败");
        }
    }

    // ══════════════════════════════════════════════════════
    // 会话重命名
    // ══════════════════════════════════════════════════════

    private void OpenRenameDialog()
    {
        if (string.IsNullOrEmpty(_selectedSessionId)) return;

        var current = _sessions.FirstOrDefault(s => s.Id == _selectedSessionId);
        _renamingSessionName = current?.DisplayName ?? "";
        _showRenameDialog = true;
        StateHasChanged();
    }

    private void CloseRenameDialog()
    {
        _showRenameDialog = false;
        _renamingSessionName = "";
        StateHasChanged();
    }

    private async Task ConfirmRenameSessionAsync()
    {
        if (string.IsNullOrEmpty(_selectedSessionId)) return;

        _isRenamingSession = true;
        StateHasChanged();

        try
        {
            var newName = _renamingSessionName?.Trim() ?? "";
            var ok = await GraphAgentApi.RenameSessionAsync(_selectedSessionId, newName);

            if (ok)
            {
                var target = _sessions.FirstOrDefault(s => s.Id == _selectedSessionId);
                if (target is not null)
                    target.DisplayName = string.IsNullOrEmpty(newName) ? null : newName;

                Logger.LogInformation("会话已重命名：{ConvId} → {Name}",
                    _selectedSessionId, newName);
                CloseRenameDialog();
            }
            else
            {
                Logger.LogWarning("重命名会话失败");
            }
        }
        finally
        {
            _isRenamingSession = false;
            StateHasChanged();
        }
    }

    private async Task HandleRenameKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await ConfirmRenameSessionAsync();
        }
        else if (e.Key == "Escape")
        {
            CloseRenameDialog();
        }
    }

    private string BuildConversationMarkdown()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("# 图 Agent 对话");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(_selectedSessionId))
            sb.AppendLine($"- **会话 ID**: `{_selectedSessionId}`");
        sb.AppendLine($"- **导出时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- **消息数**: {_messages.Count}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var m in _messages)
        {
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

            if (!string.IsNullOrEmpty(m.ImageDataUrl))
            {
                sb.AppendLine("> 📷 （用户上传了图片，Markdown 导出不包含图片内容）");
                sb.AppendLine();
            }

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

    private async Task ExportConversationAsync()
    {
        if (_messages.Count == 0) return;

        try
        {
            var markdown = BuildConversationMarkdown();

            var sessionTag = string.IsNullOrEmpty(_selectedSessionId)
                ? "new"
                : _selectedSessionId[..Math.Min(8, _selectedSessionId.Length)];
            var fileName = $"graph-chat-{sessionTag}-{DateTime.Now:yyyyMMdd-HHmmss}.md";

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
        if ((string.IsNullOrWhiteSpace(_userMessage) && string.IsNullOrEmpty(_pendingImageDataUrl))
            || _isSending)
            return;

        if (_useStreaming)
        {
            await SendMessageStreamAsync();
            return;
        }

        await SendMessageNonStreamAsync();
    }

    private async Task SendMessageNonStreamAsync()
    {
        var userText = string.IsNullOrWhiteSpace(_userMessage)
            ? "（我上传了一张图片）"
            : _userMessage.Trim();
        _userMessage = "";

        var imageDataUrl = _pendingImageDataUrl;
        _pendingImageDataUrl = null;
        _pendingImageName = null;

        _messages.Add(new ChatMsg
        {
            Role = "user",
            Text = userText,
            ImageDataUrl = imageDataUrl,
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

            // ★ 修复：用 FirstOrDefault 判重 + new SessionSummary 加入
            if (string.IsNullOrEmpty(_selectedSessionId) &&
                !string.IsNullOrEmpty(response.ConversationId))
            {
                _selectedSessionId = response.ConversationId;
                if (!_sessions.Any(s => s.Id == response.ConversationId))
                {
                    _sessions.Add(new GraphAgentSessionSummary
                    {
                        Id = response.ConversationId,
                        DisplayName = null
                    });
                }
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
            Logger.LogError(ex, "SendMessageNonStreamAsync failed");
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
        var userText = string.IsNullOrWhiteSpace(_userMessage)
            ? "（我上传了一张图片）"
            : _userMessage.Trim();
        _userMessage = "";

        var imageDataUrl = _pendingImageDataUrl;
        _pendingImageDataUrl = null;
        _pendingImageName = null;

        _messages.Add(new ChatMsg
        {
            Role = "user",
            Text = userText,
            ImageDataUrl = imageDataUrl,
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

                    // ★ 修复：用 FirstOrDefault 判重 + new SessionSummary 加入
                    if (string.IsNullOrEmpty(_selectedSessionId) &&
                        !string.IsNullOrEmpty(reply.ConversationId))
                    {
                        _selectedSessionId = reply.ConversationId;
                        if (!_sessions.Any(s => s.Id == reply.ConversationId))
                        {
                            _sessions.Add(new GraphAgentSessionSummary
                            {
                                Id = reply.ConversationId,
                                DisplayName = null
                            });
                        }
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
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Role { get; set; } = "assistant";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public List<GraphAgentToolCallDetail> ToolCallDetails { get; set; } = new();
        public string? AgentName { get; set; }
        public string? ImageDataUrl { get; set; }
    }
}