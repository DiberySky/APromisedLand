using Amazon.S3;
using FileStorageApi.Data;
using FileStorageApi.Files;
using FileStorageApi.Security;
using FileStorageApi.Storage;
using FileStorageApi.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ── 调用者上下文 ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICallerContext, HttpCallerContext>();

// ── 数据库 ──
builder.AddNpgsqlDbContext<FileStorageContext>("FileMetadataDb");

// ── SeaweedFS S3 ──
// ★ P2-11：启用配置校验，拼写错误立即 FailFast 而不是等第一次上传
builder.Services
    .AddOptions<ObjectStorageOptions>()
    .Bind(builder.Configuration.GetSection(ObjectStorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IAmazonS3>(sp =>
{
    var o = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
    var cfg = new AmazonS3Config
    {
        ServiceURL           = o.Endpoint,
        ForcePathStyle       = o.ForcePathStyle,
        UseHttp              = o.UseHttp,
        AuthenticationRegion = o.Region,
    };
    return new AmazonS3Client(o.AccessKey, o.SecretKey, cfg);
});

builder.Services.AddSingleton<IObjectStorage, S3ObjectStorage>();

// ── 业务服务 ──
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IFileMetadataService, FileMetadataService>();

// ★ 审计队列 + 后台写入（替代 FileMetadataService 中无界 Task.Run）
//   AuditQueue 单例：多请求线程写入，单一后台读者消费
//   AuditWriterService：批量落库，应用关闭时最多等待 5 秒排空
builder.Services.AddSingleton<AuditQueue>();
builder.Services.AddHostedService<AuditWriterService>();

// ★ P0-1：绑定 UploadCleanup 配置节，FileUploadService 依赖它读取阈值
builder.Services
    .AddOptions<UploadCleanupOptions>()
    .Bind(builder.Configuration.GetSection(UploadCleanupOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<UploadSessionCleanupService>();

// ── CORS ──
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:5173"];

        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FileStorageContext>();
    await db.Database.MigrateAsync();
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.UseCors("Frontend");
app.UseRouting();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();