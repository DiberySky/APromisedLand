using APromisedLand.Api.Foundries.Models;
using Microsoft.AI.Foundry.Local;
using Microsoft.AspNetCore.Mvc;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.AspNetCore.Http;

namespace APromisedLand.Api.Foundries;

public class FoundryLocalControllerBase : ControllerBase
{
    private readonly FoundryLocalManager _manager;

    protected FoundryLocalControllerBase(FoundryLocalManager manager)
    {
        _manager = manager;
    }

    [HttpGet("list")]
    public async Task<IActionResult> ListModels()
    {
        var catalog = await _manager.GetCatalogAsync();
        var models = await catalog.ListModelsAsync();

        var result = models.Select<IModel, object>(m => new
        {
            alias = m.Alias,
            displayName = m.Info?.DisplayName,
            task = m.Info?.Task,
            cached = m.IsCachedAsync().GetAwaiter().GetResult(),
            loaded = m.IsLoadedAsync().GetAwaiter().GetResult()
        });

        return Ok(result);
    }

    [HttpPost("load")]
    public async Task<IActionResult> LoadModel([FromBody] LoadModelRequest request)
    {
        var catalog = await _manager.GetCatalogAsync();
        var model = await catalog.GetModelAsync(request.ModelAlias)
            ?? throw new Exception($"Model '{request.ModelAlias}' not found.");

        if (!await model.IsCachedAsync())
            await model.DownloadAsync(p => Console.WriteLine($"下载进度: {p:F2}%"));

        await model.LoadAsync();

        return Ok(new
        {
            modelAlias = request.ModelAlias,
            status = "loaded",
            cached = await model.IsCachedAsync(),
            loaded = await model.IsLoadedAsync()
        });
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        var catalog = await _manager.GetCatalogAsync();
        var model = await catalog.GetModelAsync(request.ModelAlias)
            ?? throw new Exception($"Model '{request.ModelAlias}' not found.");

        if (!await model.IsLoadedAsync())
            await model.LoadAsync();

#pragma warning disable CS0618 // 使用过时方法但功能正常
        var chatClient = await model.GetChatClientAsync();
#pragma warning restore CS0618

        var messages = request.Messages.Select(m => new ChatMessage
        {
            Role = m.Role,
            Content = m.Content
        }).ToList();

        var response = await chatClient.CompleteChatAsync(messages);

        var content = response.Choices?.FirstOrDefault()?.Message?.Content ?? "No response";

        return Ok(new ChatResponse(content, request.ModelAlias, response.Usage?.TotalTokens));
    }

    [HttpPost("chat/stream")]
    public async Task ChatStream([FromBody] ChatRequest request)
    {
        var catalog = await _manager.GetCatalogAsync();
        var model = await catalog.GetModelAsync(request.ModelAlias)
            ?? throw new Exception($"Model '{request.ModelAlias}' not found.");

        if (!await model.IsLoadedAsync())
            await model.LoadAsync();

#pragma warning disable CS0618
        var chatClient = await model.GetChatClientAsync();
#pragma warning restore CS0618

        var messages = request.Messages.Select(m => new ChatMessage
        {
            Role = m.Role,
            Content = m.Content
        }).ToList();

        Response.ContentType = "text/event-stream";

        var streamingResponse = chatClient.CompleteChatStreamingAsync(messages, HttpContext.RequestAborted);

        await foreach (var chunk in streamingResponse)
        {
            var chunkContent = chunk.Choices?.FirstOrDefault()?.Message?.Content;
            if (!string.IsNullOrEmpty(chunkContent))
            {
                await Response.WriteAsync("data: " + chunkContent + "\n\n");
                await Response.Body.FlushAsync();
            }
        }

        await Response.WriteAsync("data: [DONE]\n\n");
    }

    [HttpPost("unload")]
    public async Task<IActionResult> UnloadModel([FromBody] LoadModelRequest request)
    {
        var catalog = await _manager.GetCatalogAsync();
        var model = await catalog.GetModelAsync(request.ModelAlias)
            ?? throw new Exception($"Model '{request.ModelAlias}' not found.");

        await model.UnloadAsync();

        return Ok(new { modelAlias = request.ModelAlias, status = "unloaded" });
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "ok" });
    }
}