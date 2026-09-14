using System.Text.Json;
using MafStatefulApi.State;
using Microsoft.Agents.AI;
using StackExchange.Redis;

namespace MafStatefulApi.Agents;

/// <summary>
/// Orchestrates agent execution with session persistence.
/// Loads chat history state, runs the agent, and saves the updated state.
/// 同一 conversationId 的请求通过 Redis 分布式锁串行化，防止并发覆盖。
/// </summary>
public class AgentRunner(
    [FromKeyedServices("AssistantAgent")] AIAgent agent,
    IAgentSessionStore sessionStore,
    IConnectionMultiplexer redis,
    ILogger<AgentRunner> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    // 锁 TTL：既当"最长等待时间"，也当"意外崩溃时的兜底释放时间"
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(100);

    // 释放锁的 Lua 脚本：只有持有者才能释放，避免误删他人锁
    private const string ReleaseLockScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    public async Task<string> RunAsync(
        string conversationId,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var lockKey = $"maf:sessions:lock:{conversationId}";
        var lockToken = Guid.NewGuid().ToString("N");
        var db = redis.GetDatabase();

        if (!await TryAcquireLockAsync(db, lockKey, lockToken, cancellationToken))
        {
            throw new TimeoutException(
                $"Could not acquire lock for conversation '{conversationId}' within {LockTtl}.");
        }

        try
        {
            return await RunCoreAsync(conversationId, userMessage, cancellationToken);
        }
        finally
        {
            await ReleaseLockAsync(db, lockKey, lockToken);
        }
    }

    private static async Task<bool> TryAcquireLockAsync(
        IDatabase db, string key, string token, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + LockTtl;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            if (await db.StringSetAsync(key, token, LockTtl, When.NotExists))
            {
                return true;
            }

            await Task.Delay(LockRetryDelay, ct);
        }
        return false;
    }

    private static async Task ReleaseLockAsync(IDatabase db, string key, string token)
    {
        try
        {
            await db.ScriptEvaluateAsync(
                ReleaseLockScript,
                keys: new RedisKey[] { key },
                values: new RedisValue[] { token });
        }
        catch (Exception)
        {
            // 释放失败不阻断主流程；锁会在 TTL 到期后自动过期
        }
    }

    private async Task<string> RunCoreAsync(
        string conversationId,
        string userMessage,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Running agent for conversation {ConversationId}",
            conversationId);

        var session = await LoadOrCreateSessionAsync(conversationId, cancellationToken);

        var response = await agent.RunAsync(userMessage, session, cancellationToken: cancellationToken);
        var answer = response.Text ?? string.Empty;

        await SaveSessionAsync(conversationId, session, cancellationToken);

        logger.LogInformation(
            "Agent response for conversation {ConversationId}: {ResponseLength} chars",
            conversationId,
            answer.Length);

        return answer;
    }

    private async Task<AgentSession> LoadOrCreateSessionAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        var serializedSession = await sessionStore.GetAsync(conversationId, cancellationToken);

        if (serializedSession is not null)
        {
            logger.LogDebug(
                "Deserializing existing session for conversation {ConversationId}",
                conversationId);

            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(serializedSession, JsonOptions);
                return await agent.DeserializeSessionAsync(jsonElement, JsonOptions, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to deserialize session for conversation {ConversationId}, creating new session",
                    conversationId);
            }
        }

        logger.LogDebug(
            "Creating new session for conversation {ConversationId}",
            conversationId);

        return await agent.CreateSessionAsync(cancellationToken);
    }

    private async Task SaveSessionAsync(
        string conversationId,
        AgentSession session,
        CancellationToken cancellationToken)
    {
        var serializedElement = await agent.SerializeSessionAsync(session, JsonOptions, cancellationToken);
        var serialized = serializedElement.GetRawText();

        logger.LogDebug(
            "Saving session for conversation {ConversationId}, serialized size: {SizeBytes} bytes",
            conversationId,
            serialized.Length);

        await sessionStore.SetAsync(conversationId, serialized, cancellationToken);
    }
}