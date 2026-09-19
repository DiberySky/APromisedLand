using DiberyBlazorWebSky.Models.Graph;
using DiberyBlazorWebSky.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace DiberyBlazorWebSky.Components.Pages;

public partial class McpGraphChat : ComponentBase, IDisposable
{
    // ─── 注入 ─────────────────────────────────────────
    [Inject] private ChatApiClient ChatApi { get; set; } = default!;
    [Inject] private GraphApiClient GraphApi { get; set; } = default!;
    [Inject] private GraphDynamicContextService GraphDynamicContext { get; set; } = default!;
    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private ILogger<McpGraphChat> Logger { get; set; } = default!;

    // ─── 会话状态 ─────────────────────────────────────
    private readonly List<GraphChatMessage> _messages = new();
    private List<string> _sessions = new();
    private string _userMessage = "";
    private string _selectedSessionId = "";
    private bool _isSending;
    private bool _isLoadingSessions;
    private int _elapsedSeconds;
    private ElementReference _messagesContainer;

    // ─── 图上下文状态 ────────────────────────────────
    private List<GraphDto> _graphs = new();
    private string _selectedGraphGuid = "";
    private bool _useGraphContext;
    private int _graphContextNodeCount = -1;
    private int _graphContextEdgeCount = -1;

    private CancellationTokenSource? _sendCts;
    private Timer? _elapsedTimer;
    private DateTime _sendStartedAt;

    // ─── 生命周期 ─────────────────────────────────────
    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(LoadSessionsAsync(), LoadGraphsAsync());
    }

    // ─── 图列表 ───────────────────────────────────────
    private async Task LoadGraphsAsync()
    {
        try
        {
            _graphs = (await GraphApi.ListGraphsAsync()).ToList();

            if (_graphs.Count > 0 &&
                (string.IsNullOrEmpty(_selectedGraphGuid) ||
                 !_graphs.Any(g => g.Guid.ToString() == _selectedGraphGuid)))
            {
                _selectedGraphGuid = _graphs[0].Guid.ToString();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "加载图列表失败");
        }
    }

    // ─── 会话管理 ─────────────────────────────────────
    private async Task LoadSessionsAsync()
    {
        if (_isLoadingSessions) return;

        _isLoadingSessions = true;
        StateHasChanged();

        try
        {
            var list = await ChatApi.ListSessionsAsync();
            _sessions = list.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load sessions");
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
        _userMessage = "";

        if (!string.IsNullOrEmpty(newId))
        {
            try
            {
                var history = await ChatApi.GetSessionMessagesAsync(newId);
                foreach (var msg in history)
                {
                    _messages.Add(new GraphChatMessage
                    {
                        Role = NormalizeRole(msg.Role),
                        Text = msg.Text,
                        Timestamp = DateTime.Now
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "加载会话历史失败：{ConversationId}", newId);
                _messages.Add(new GraphChatMessage
                {
                    Role = "error",
                    Text = "加载历史消息失败，请重试。",
                    Timestamp = DateTime.Now
                });
            }
        }

        await ScrollToBottomAsync();
        StateHasChanged();
    }

    private static string NormalizeRole(string role)
    {
        return role.ToLowerInvariant() switch
        {
            "user" => "user",
            "assistant" => "assistant",
            "system" => "system",
            _ => "assistant"
        };
    }

    // ─── 图上下文变更 ─────────────────────────────────
    private async Task OnGraphContextChangedAsync(ChangeEventArgs e)
    {
        _selectedGraphGuid = e.Value?.ToString() ?? "";
        await LoadGraphContextSummaryAsync();
    }

    private async Task LoadGraphContextSummaryAsync()
    {
        if (_useGraphContext && !string.IsNullOrEmpty(_selectedGraphGuid))
        {
            try
            {
                var guid = Guid.Parse(_selectedGraphGuid);
                var (nodes, edges) = await LoadGraphDataAsync(guid);
                _graphContextNodeCount = nodes.Count;
                _graphContextEdgeCount = edges.Count;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "预加载图数据失败");
                _graphContextNodeCount = -1;
                _graphContextEdgeCount = -1;
            }
        }
        else
        {
            _graphContextNodeCount = -1;
            _graphContextEdgeCount = -1;
        }

        StateHasChanged();
    }

    private async Task<(List<NodeDto> nodes, List<EdgeDto> edges)> LoadGraphDataAsync(Guid graphGuid)
    {
        var nodeTask = GraphApi.ListNodesAsync(graphGuid);
        var edgeTask = GraphApi.ListEdgesAsync(graphGuid);
        await Task.WhenAll(nodeTask, edgeTask);

        return (
            nodeTask.Result.ToList(),
            edgeTask.Result.ToList()
        );
    }

    // ─── 发送消息 ─────────────────────────────────────
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(_userMessage) || _isSending) return;

        var originalText = _userMessage.Trim();
        _userMessage = "";

        var textToSend = originalText;
        string? contextLabel = null;

        if (_useGraphContext && !string.IsNullOrEmpty(_selectedGraphGuid))
        {
            try
            {
                var graphGuid = Guid.Parse(_selectedGraphGuid);
                var graphName = _graphs.FirstOrDefault(g => g.Guid == graphGuid)?.Name ?? "Graph";

                var (dynamicContext, label) = await GraphDynamicContext.BuildAsync(
                    graphGuid, graphName, originalText);

                // ★ 使用 string.Join 拼接，避免在 .razor 中触发 Razor 解析器
                textToSend = string.Join("\n", new[]
                {
                    "以下是当前图的上下文。请基于这些真实数据回答用户问题。",
                    "如果上下文中不包含答案，请明确说明，不要编造。",
                    "",
                    dynamicContext,
                    "",
                    $"用户问题：{originalText}"
                });

                contextLabel = label;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "动态上下文构建失败，降级为普通消息");
                contextLabel = null;
            }
        }

        _messages.Add(new GraphChatMessage
        {
            Role = "user",
            Text = originalText,
            Timestamp = DateTime.Now,
            GraphContextLabel = contextLabel
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
            var response = await ChatApi.SendAsync(
                textToSend,
                string.IsNullOrEmpty(_selectedSessionId) ? null : _selectedSessionId,
                _sendCts.Token);

            if (response is null)
            {
                _messages.Add(new GraphChatMessage
                {
                    Role = "error",
                    Text = "API 返回空响应，请查看服务端日志后重试。",
                    Timestamp = DateTime.Now
                });
                return;
            }

            if (string.IsNullOrEmpty(_selectedSessionId))
            {
                _selectedSessionId = response.ConversationId;
                await LoadSessionsAsync();
            }

            _messages.Add(new GraphChatMessage
            {
                Role = "assistant",
                Text = response.Reply,
                Timestamp = DateTime.Now
            });
        }
        catch (OperationCanceledException)
        {
            _messages.Add(new GraphChatMessage
            {
                Role = "error",
                Text = "已取消。",
                Timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SendMessageAsync failed");
            _messages.Add(new GraphChatMessage
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

    private void CancelAsync()
    {
        _sendCts?.Cancel();
    }

    private async Task ResetSessionAsync()
    {
        if (string.IsNullOrEmpty(_selectedSessionId) || _isSending) return;

        try
        {
            await ChatApi.ResetAsync(_selectedSessionId);

            _messages.Clear();
            _selectedSessionId = "";
            await LoadSessionsAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Reset failed for {ConversationId}", _selectedSessionId);
            _messages.Add(new GraphChatMessage
            {
                Role = "error",
                Text = ex.Message,
                Timestamp = DateTime.Now
            });
        }
        finally
        {
            StateHasChanged();
        }
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
        {
            await SendMessageAsync();
        }
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
            // 预渲染阶段 JS interop 不可用，忽略
        }
    }

    private static string GetAvatar(string role)
    {
        return role switch
        {
            "user" => "👤",
            "error" => "⚠️",
            _ => "🤖"
        };
    }

    public void Dispose()
    {
        _elapsedTimer?.Dispose();
        _sendCts?.Cancel();
        _sendCts?.Dispose();
    }
}