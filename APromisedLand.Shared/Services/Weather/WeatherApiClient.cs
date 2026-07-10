using System.Net.Http.Json;

namespace APromisedLand.Shared.Services.Weather;

public class WeatherApiClient
{
    private readonly HttpClient _httpClient;

    // HttpClient 由 DI 自动注入，并且已配置 BaseAddress 和 JwtAuthorizationMessageHandler
    public WeatherApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<WeatherForecast[]?> GetForecastsAsync()
    {
        return await _httpClient.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast");
    }
}