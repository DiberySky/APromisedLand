using NebulaGraphApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

// 加载业务配置（SpaceName 等）
builder.Services.Configure<NebulaGraphOptions>(builder.Configuration.GetSection("NebulaGraph"));

// 按 README 注册 NebulaNet 连接池
var nebulaOptions = builder.Configuration.GetSection("NebulaGraph").Get<NebulaGraphOptions>()!;
builder.Services.AddNebulaGraph(config =>
{
    config.Ip = nebulaOptions.Host;
    config.Port = nebulaOptions.Port;
});


// 注册自定义客户端和 Seed
builder.Services.AddSingleton<INebulaGraphClient, NebulaGraphNetClient>();
builder.Services.AddSingleton<NebulaGraphSeedService>();

// 加载配置（appsettings.json + 环境变量）
// builder.Services.Configure<NebulaGraphOptions>(builder.Configuration.GetSection("NebulaGraph"));
// builder.Services.AddSingleton<INebulaGraphClient, NebulaGraphNetClient>(); 
// builder.Services.AddSingleton<NebulaGraphSeedService>(); 

var app = builder.Build();

// ****** 执行 Seed（支持配置开关） ******
var seedEnabled = app.Configuration.GetValue<bool>("Seed:Enabled", true);
if (seedEnabled)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<NebulaGraphSeedService>();
    try
    {
        await seeder.SeedAsync();
        app.Logger.LogInformation("✅ NebulaGraph Seed 执行成功");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "❌ NebulaGraph Seed 执行失败，应用将继续启动");
        // 若希望种子失败时阻止启动，可在此处重新抛出异常
    }
}
else
{
    app.Logger.LogInformation("⏭️ NebulaGraph Seed 已禁用");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapDefaultEndpoints();

app.Run();