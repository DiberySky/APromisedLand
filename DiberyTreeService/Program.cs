using APromisedLand.Api.Projects.DiberyTree.Services;
using APromisedLand.Shared.DiberyTree.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

// 注册泛型树服务（以 string 类型为例）
builder.Services.AddSingleton<ITreeService<string>, TreeService<string>>();

// 如果需要多种类型的树，可以分别注册
// builder.Services.AddSingleton<ITreeService<MyCustomType>, TreeService<MyCustomType>>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();