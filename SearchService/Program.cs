using System.Text.RegularExpressions;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SearchService.Data;
using SearchService.Models;
using Typesense;
using Typesense.Setup;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddServiceDefaults();

// builder.Services.AddScoped<ITypesenseClient, TypesenseClient>();

var typesenseUri = builder.Configuration["services:typesense:typesense:0"];
if (string.IsNullOrEmpty(typesenseUri))
    throw new InvalidOperationException("配置中未找到 Typesense URI。");

var typesenseApiKey = builder.Configuration["typesense-api-key"];
if (string.IsNullOrEmpty(typesenseApiKey))
    throw new InvalidOperationException("配置中未找到 Typesense API 密钥");

var uri = new Uri(typesenseUri);
builder.Services.AddTypesenseClient(config =>
{
    config.ApiKey = typesenseApiKey;
    config.Nodes = new List<Node>
    {
        new(uri.Host, uri.Port.ToString(), uri.Scheme)
    };
});

builder.Services.AddOpenTelemetry().WithTracing(traceProviderBuilder =>
{
    traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(builder.Environment.ApplicationName))
        .AddSource("Wolverine");
});

builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMqUsingNamedConnection("Messaging").AutoProvision();
    opts.ListenToRabbitQueue("questions.search", cfg =>
    {
        cfg.BindExchange("questions");
    });
    
    // 为 ITypesenseClient 启用服务定位
    opts.CodeGeneration.AlwaysUseServiceLocationFor<ITypesenseClient>();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapGet("/search", async (string query, ITypesenseClient client) =>
{
    //[aspire]something
    string? tag = null;
    var tagMatch = Regex.Match(query, @"\[(.*?)\]");
    if (tagMatch.Success)
    {
        tag = tagMatch.Groups[1].Value;
        query = query.Replace(tagMatch.Value, "").Trim();
    }
    
    var searchParams = new SearchParameters(query, "title,content");

    if (!string.IsNullOrWhiteSpace(tag))
    {
        searchParams.FilterBy = $"tags:=[{tag}]]";
    }

    try
    {
        var result = await client.Search<SearchQuestion>("questions", searchParams);
        return Results.Ok(result.Hits.Select(hit => hit.Document));
    }
    catch (Exception e)
    {
        return Results.Problem("Typesense 搜索失败", e.Message);
    }
});

app.MapGet("/search/similar-titles", async (string query, ITypesenseClient client) =>
{
    var searchParams = new SearchParameters(query, "title");

    try
    {
        var result = await client.Search<SearchQuestion>("questions", searchParams);
        return Results.Ok(result.Hits.Select(hit => hit.Document));
    }
    catch (Exception e)
    {
        return Results.Problem("Typesense 搜索失败", e.Message);
    }
});
    
using var scope = app.Services.CreateScope();
var client = scope.ServiceProvider.GetRequiredService<ITypesenseClient>();
await SearchInitializer.EnsureIndexExists(client);

app.Run();
