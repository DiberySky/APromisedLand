using Microsoft.AspNetCore.Mvc;

namespace OllamaService.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController(EmbeddingService embed) : ControllerBase
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    [HttpGet(Name = "GetWeatherForecast")]
    public async Task<IEnumerable<WeatherForecast>> Get()
    {
        List<string> texts = ["需要生成向量的文本", "为这个句子生成表示以用于检索相关文章："];
        var vectors = await embed.GenerateEmbedding(texts);
        
        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
    }
}