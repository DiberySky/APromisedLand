namespace APromisedLand.Shared.Services.Solution;

public partial class SolutionService
{
    public static string Version { get; set; } = "2026.06.23.01";

    public static bool Debug { get; set; } = false;

    public static float MinScore { get; set; } = 0.80f; //只返回相似度 ≥ 0.80 的文档

    public static string Copyright { get; set; } = $"\u00A9 {DateTime.Now.Year} Wuhan Horizon Technology Co., Ltd.";
}
