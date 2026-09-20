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

    protected override async Task OnInitializedAsync()
    {
        await LoadSessionsAsync();
    }

    // ─── 会话列表 ─────────────────────────────────────

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

    // ─── 切换会话：加载历史消息 ──────────────────────

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
                    Timestamp = DateTime.Now  // 历史消息没有时间戳，用当前时间
                });
            }
            Logger.LogInformation("加载会话 {ConvId} 的 {Count} 条历史消息",
                newId, history.Count);
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

    // ─── 重置会话 ─────────────────────────────────────

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

    // ─── 发送消息 ─────────────────────────────────────

    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(_userMessage) || _isSending) return;

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

            // ★ 首次发消息：把新会话 ID 加入列表并选中
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
                ToolsInvoked = response.ToolsInvoked
            });
        }
        catch (OperationCanceledException)
        {
            _messages.Add(new ChatMsg
            {
                Role = "error",
                Text = "已取消。",
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SendMessageAsync failed");
            _messages.Add(new ChatMsg
            {
                Role = "error",
                Text = ex.Message,
                Timestamp = DateTime.Now
            });
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
        catch { }
    }

    private static string GetAvatar(string role) => role switch
    {
        "user" => "👤",
        "error" => "⚠️",
        _ => "🤖"
    };

    public void Dispose()
    {
        _elapsedTimer?.Dispose();
        _sendCts?.Cancel();
        _sendCts?.Dispose();
    }

    private sealed class ChatMsg
    {
        public string Role { get; set; } = "assistant";
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public List<string> ToolsInvoked { get; set; } = new();
    }
}