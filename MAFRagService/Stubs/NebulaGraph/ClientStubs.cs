// 桩代码：替代不存在的 NebulaGraph.Net NuGet 包
// 实际使用时替换为真实的 NebulaGraph .NET 客户端

namespace MAFRagService.Stubs.NebulaGraph;

public class NebulaGraphOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public static NebulaGraphOptions FromConnectionString(string connectionString)
    {
        return new NebulaGraphOptions { ConnectionString = connectionString };
    }
}

public class NebulaGraphClient
{
    private NebulaGraphOptions _options;
    private string _currentSpace = "";

    public NebulaGraphClient(NebulaGraphOptions options)
    {
        _options = options;
    }

    public Task<ResultSet> ExecuteAsync(string ngql)
    {
        return ExecuteAsync(ngql, CancellationToken.None);
    }

    public Task<ResultSet> ExecuteAsync(string ngql, CancellationToken ct)
    {
        // 桩实现：返回空成功结果
        // 桩实现：显式失败，避免误导性日志
        // return Task.FromResult(new ResultSet
        // {
        //     IsSucceeded  = false,
        //     ErrorMessage = "NebulaGraphClient 是桩实现，未连接真实 Nebula。"
        // });
        
        return Task.FromResult(new ResultSet());
        
    }

    public Task ChangeSpaceAsync(string space)
    {
        return ChangeSpaceAsync(space, CancellationToken.None);
    }

    public Task ChangeSpaceAsync(string space, CancellationToken ct)
    {
        _currentSpace = space;
        return Task.CompletedTask;
    }
}
