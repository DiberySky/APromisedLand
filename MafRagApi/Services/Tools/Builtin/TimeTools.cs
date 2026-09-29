using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace MafRagApi.Services.Tools;

public sealed class TimeTools
{
    [Description("获取当前 UTC 时间（ISO 8601 格式）。")]
    public string GetUtcNow() => DateTimeOffset.UtcNow.ToString("O");

    [Description("获取指定 IANA 时区的当前时间。")]
    public string GetTimeInZone(
        [Description("IANA 时区 ID，例如 Asia/Shanghai、America/New_York。")] string timeZoneId)
    {
        try
        {
            var tz  = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            return now.ToString("O");
        }
        catch (Exception ex)
        {
            return $"错误: 无效时区 '{timeZoneId}' — {ex.Message}";
        }
    }

    public IReadOnlyList<ToolDescriptor> GetTools()
    {
        var fnNow = AIFunctionFactory.Create(
            GetUtcNow,
            name: "get_utc_now",
            description: "获取当前 UTC 时间（ISO 8601 格式）。");

        var fnZone = AIFunctionFactory.Create(
            GetTimeInZone,
            name: "get_time_in_zone",
            description: "获取指定 IANA 时区的当前时间。");

        return new[]
        {
            new ToolDescriptor
            {
                Name = "get_utc_now", Function = fnNow,
                Description = "获取当前 UTC 时间",
                Tags = new[] { "time", "safe" },
                IsBase = true,
            },
            new ToolDescriptor
            {
                Name = "get_time_in_zone", Function = fnZone,
                Description = "获取指定时区当前时间",
                Tags = new[] { "time", "safe" },
                IsBase = true,
            },
        };
    }
}