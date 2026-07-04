using FileTransService.Data;
using FileTransService.Models;
using FileTransService.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<FileTransDbContext>("fileTransDb");

builder.Services.AddHttpClient<ISeaweedFsClient, SeaweedFsClient>();
builder.Services.Configure<SeaweedFsOptions>(builder.Configuration.GetSection("SeaweedFS"));
builder.Services.AddScoped<IFileService, FileService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapDefaultEndpoints();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    var context = services.GetRequiredService<FileTransDbContext>();
    await context.Database.MigrateAsync();
}
catch (Exception e)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(e, "在迁移或初始化数据库时出现了错误。");
}

app.Run();