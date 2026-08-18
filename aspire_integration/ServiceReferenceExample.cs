// 在其他 .NET 服务中引用 NebulaGraphApiService 示例

// 1. 在 AppHost 中注册引用
// builder.AddDiberyTreeService(context)
//     .WithReference(context.NebulaGraphApi)  // 自动注入 NEBULAGRAPHAPI_HTTP 环境变量
//     .WaitFor(context.NebulaGraphApi);

// 2. 在 .NET 服务中消费（Program.cs）
// var nebulaApiUrl = builder.Configuration["NEBULAGRAPHAPI_HTTP"];
// services.AddHttpClient("NebulaGraphApi", client =>
// {
//     client.BaseAddress = new Uri(nebulaApiUrl);
// });

// 3. 调用示例
// var client = httpClientFactory.CreateClient("NebulaGraphApi");
// var response = await client.PostAsJsonAsync("/api/v1/graph/query", new
// {
//     space = "basketballplayer",
//     query = "MATCH (v:player) RETURN v.player.name LIMIT 10"
// });
