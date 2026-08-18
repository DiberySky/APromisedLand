using APromisedLand.AppHost;
using APromisedLand.AppHost.Extensions;
using Aspire.Hosting.DevTunnels;

var builder = DistributedApplication.CreateBuilder(args);

var context = new AppHostContext();

// 基础设施层
builder.AddPostgres(context);           // PostgreSQL
builder.AddRedis(context);              // Redis
builder.AddNebulaGraph(context);        // NebulaGraph 图数据库集群 (MetaD/StorageD/GraphD)

// 服务层
builder.AddNebulaGraphApiService(context);  // ← Python FastAPI 图数据库 API 服务
builder.AddNebulaStudio(context);           // NebulaStudio Web UI

// 业务层
builder.AddDiberyTreeService(context);
builder.AddDiberyMauiSky(context);

builder.Build().Run();
