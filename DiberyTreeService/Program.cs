using APromisedLand.Api.Data;
using APromisedLand.Api.DiberyTree.Interface;
using APromisedLand.Api.DiberyTree.Services;
using APromisedLand.Api.Interfaces;
using APromisedLand.Api.Services;
using APromisedLand.Shared.DiberyTree.Interfaces;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<DiberyDbContext>("TreeDb");

// 注册泛型树服务（以 string 类型为例）
// builder.Services.AddSingleton<ITreeService<string>, TreeService<string>>();

// 如果需要多种类型的树，可以分别注册
builder.Services.AddScoped<ITreeService<CategoryTree>, CategoryTreeService>();
builder.Services.AddScoped<ITreeService<UnitTree>, UnitTreeService>();

//ITreeAttributeService 
builder.Services.AddScoped<ITreeAttributeService, TreeAttributeService>();
builder.Services.AddScoped<IUnitOfMeasureService, UnitOfMeasureService>();

builder.Services.AddScoped<AttributeDefinitionService>();
builder.Services.AddScoped<AttributeTableValueService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapDefaultControllerRoute();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    var context = services.GetRequiredService<DiberyDbContext>();
    await context.Database.MigrateAsync();
}
catch (Exception e)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(e, "在迁移或初始化数据库时出现了错误。");
}

app.Run();