// using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Nebula.Graph;
using NebulaNet;

namespace NebulaGraphApiService.Services;

public class NebulaGraphNetClient(
    NebulaPool pool,
    IOptions<NebulaGraphOptions> options,
    ILogger<NebulaGraphNetClient> logger)
    : INebulaGraphClient
{
    private readonly NebulaGraphOptions _options = options.Value;

    private async Task<ExecutionResponse> ExecuteInternalAsync(
        string ngql,
        CancellationToken cancellationToken,
        bool useSpace = false)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 3.0 API: GetSessionAsync 需要用户名和密码
        var session = await pool.GetSessionAsync();
        try
        {
            if (useSpace && !string.IsNullOrEmpty(_options.SpaceName))
            {
                await session.ExecuteAsync($"USE `{_options.SpaceName}`");
            }

            return await session.ExecuteAsync(ngql);
        }
        finally
        {
            session.Release();
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await ExecuteInternalAsync("SHOW SPACES", cancellationToken, useSpace: false);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Test connection failed");
            return false;
        }
    }

    public async Task<bool> SpaceExistsAsync(string spaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            throw new NotImplementedException();
            
            // var result = await ExecuteInternalAsync("SHOW SPACES", cancellationToken, useSpace: false);
            // // 3.0 API: 使用 ResultSet 的方法解析数据
            // var rows = result.AsStringTable();
            // return rows.Skip(1).Any(r => r.Length > 0 && r[0] == spaceName);
        }
        catch
        {
            return false;
        }
    }

    public async Task CreateSpaceAsync(string spaceName, CancellationToken cancellationToken = default)
    {
        var ngql = $"CREATE SPACE IF NOT EXISTS `{spaceName}` (vid_type=FIXED_STRING(32))";
        await ExecuteInternalAsync(ngql, cancellationToken, useSpace: false);
        logger.LogInformation("Space {SpaceName} created.", spaceName);
    }

    public async Task<bool> SpaceReadyAsync(string spaceName, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await pool.GetSessionAsync();
            try
            {
                throw new NotImplementedException();
                // await session.ExecuteAsync($"USE `{spaceName}`");
                // var result = await session.ExecuteAsync("SHOW TAGS");
                // // 检查是否成功执行
                // return result.IsSucceed();
            }
            finally
            {
                session.Release();
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Space {SpaceName} is not ready", spaceName);
            return false;
        }
    }

    public async Task UseSpaceAsync(string spaceName, CancellationToken cancellationToken = default)
    {
        await ExecuteInternalAsync($"USE `{spaceName}`", cancellationToken, useSpace: false);
        logger.LogDebug("Switched to space {SpaceName}.", spaceName);
    }

    public async Task<bool> TagExistsAsync(string tagName, CancellationToken cancellationToken = default)
    {
        try
        {
            throw new NotImplementedException();
            // var result = await ExecuteInternalAsync("SHOW TAGS", cancellationToken, useSpace: true);
            // var rows = result.AsStringTable();
            // return rows.Skip(1).Any(r => r.Length > 0 && r[0] == tagName);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EdgeExistsAsync(string edgeName, CancellationToken cancellationToken = default)
    {
        try
        {
            throw new NotImplementedException();
            // var result = await ExecuteInternalAsync("SHOW EDGES", cancellationToken, useSpace: true);
            // var rows = result.AsStringTable();
            // return rows.Skip(1).Any(r => r.Length > 0 && r[0] == edgeName);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IndexExistsAsync(string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var showCmd in new[] { "SHOW TAG INDEXES", "SHOW EDGE INDEXES" })
            {
                throw new NotImplementedException();
                // var result = await ExecuteInternalAsync(showCmd, cancellationToken, useSpace: true);
                // var rows = result.AsStringTable();
                // if (rows.Skip(1).Any(r => r.Length > 0 && r[0] == indexName))
                    return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> VertexExistsAsync(string vid, CancellationToken cancellationToken = default)
    {
        try
        {
            throw new NotImplementedException();
            var result = await ExecuteInternalAsync($"FETCH PROP ON * {vid}", cancellationToken, useSpace: true);
            // return result.GetRowSize() > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EdgeExistsBetweenAsync(
        string src,
        string dst,
        string edgeName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            throw new NotImplementedException();
            // var result = await ExecuteInternalAsync(
            //     $"FETCH PROP ON `{edgeName}` {src} -> {dst}",
            //     cancellationToken,
            //     useSpace: true);
            // return result.GetRowSize() > 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task ExecuteAsync(string statement, CancellationToken cancellationToken = default)
    {
        await ExecuteInternalAsync(statement, cancellationToken, useSpace: false);
        logger.LogTrace("Executed: {Statement}", statement);
    }
}