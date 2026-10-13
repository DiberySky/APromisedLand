using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TreeGraph.Shared.NodeEavSky.Dtos;

namespace TreeGraph.Api.NodeEavSky.Controllers;

/// <summary>
/// 逆地理编码代理：GCJ-02 坐标 → 地名 / 地址（高德 Web 服务）。
///
/// Key 存服务端配置 Amap:WebServiceKey，避免暴露到浏览器并绕开跨域。
/// 未配置 Key 时返回 400，前端静默降级为只回填坐标。
/// </summary>
[ApiController]
[Route("api/eav/geo")]
public class GeoController : ControllerBase
{
    // 静态 HttpClient：避免每次新建产生套接字耗尽；对高德 API 无连接复用顾虑
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConfiguration _config;

    public GeoController(IConfiguration config) => _config = config;

    [HttpGet("regeo")]
    public async Task<IActionResult> Regeo(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken ct)
    {
        var key = _config["Amap:WebServiceKey"];
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { error = "未配置高德 Web 服务 Key（Amap:WebServiceKey）" });

        var location = FormattableString.Invariant($"{longitude:0.######},{latitude:0.######}");
        var url = "https://restapi.amap.com/v3/geocode/regeo"
                  + $"?key={key}&location={location}&extensions=all&radius=200";

        using var doc = await Http.GetFromJsonAsync<JsonDocument>(url, JsonOptions, ct);
        var root = doc?.RootElement ?? default;

        if (doc is null
            || root.GetProperty("status").GetString() != "1")
        {
            var info = doc is not null && root.TryGetProperty("info", out var i)
                ? i.GetString() : "未知错误";
            return BadRequest(new { error = $"高德逆地理编码失败：{info}" });
        }

        var regeocode = root.GetProperty("regeocode");

        string? address = regeocode.TryGetProperty("formatted_address", out var fa)
            ? fa.GetString() : null;

        // 最近 POI 名称作为地点名称建议（extensions=all 才有 pois）
        string? label = null;
        if (regeocode.TryGetProperty("pois", out var pois)
            && pois.ValueKind == JsonValueKind.Array
            && pois.GetArrayLength() > 0
            && pois[0].TryGetProperty("name", out var name))
        {
            label = name.GetString();
        }

        return Ok(new RegeoResultDto(label, address));
    }
}
