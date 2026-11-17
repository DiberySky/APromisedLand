namespace TreeGraph.Api.MessagingSky;

/// <summary>收集必须在当前请求数据库事务成功提交后执行的旁路动作。</summary>
public sealed class TreeGraphRequestTransaction
{
    private readonly List<Func<CancellationToken, Task>> _postCommitActions = [];
    private bool _managed;

    internal void BeginManaged() => _managed = true;

    public async Task AfterCommitAsync(
        Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (!_managed)
        {
            // Testing 与非 HTTP 写路径没有请求事务中间件，保持原有 SaveChanges 后执行语义。
            await action(ct);
            return;
        }

        _postCommitActions.Add(action);
    }

    internal async Task RunPostCommitAsync(CancellationToken ct)
    {
        foreach (var action in _postCommitActions)
            await action(ct);
    }
}
