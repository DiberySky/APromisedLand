using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace APromisedLand.AppHost;

public class AppHostContext
{
    // 数据库
    public IResourceBuilder<PostgresServerResource>? Postgres { get; set; }
    public IResourceBuilder<PostgresDatabaseResource>? PostgresDb { get; set; }
    public IResourceBuilder<RedisResource>? Redis { get; set; }

    // NebulaGraph
    public IResourceBuilder<ContainerResource>? NebulaGraph { get; set; }
    public EndpointReference? NebulaGraphEndpoint { get; set; }
    public IResourceBuilder<ContainerResource>? NebulaConsole { get; set; }

    // ← 新增：Python FastAPI 服务引用
    public IResourceBuilder<UvicornAppResource>? NebulaGraphApi { get; set; }
    public EndpointReference? NebulaGraphApiEndpoint { get; set; }

    // 其他服务...
    public IResourceBuilder<ContainerResource>? NebulaStudio { get; set; }
}
