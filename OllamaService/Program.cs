using Microsoft.Extensions.AI;
using OllamaService;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

// // Register IEmbeddingGenerator
builder.AddOllamaApiClient("embedding")
    .AddEmbeddingGenerator();

builder.Services.AddScoped<EmbeddingService>();

// Register IChatClient
// builder.AddOllamaApiClient("ollama-llama3")
//     .AddChatClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapDefaultEndpoints();

// app.MapPost("/embed", async (OllamaApiClient ollama, EmbedRequestDto request) =>
// {
//     // 直接批处理所有文本，一次请求生成所有嵌入
//     var response = await ollama.EmbedAsync(new EmbedRequest
//     {
//         Model = "bge-large",
//         Input = request.Texts.ToList()  // Input 接受 string[]，一次生成多个向量
//     });
//
//     var embeddings = response.Embeddings; // 返回 List<float[]> 或 float[][]
//     return Results.Ok(new { embeddings });
// });

app.Run();

public record EmbedRequestDto(string[] Texts);
