using System.Net.Http.Json;

namespace APromisedLand.MauiBlazor.Weather;

public class WeatherApiClient(HttpClient httpClient)
{
    // HttpClient 由 DI 自动注入，并且已配置 BaseAddress 和 JwtAuthorizationMessageHandler

    public async Task<WeatherForecast[]?> GetForecastsAsync()
    {
        return await httpClient.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast");
    }
}