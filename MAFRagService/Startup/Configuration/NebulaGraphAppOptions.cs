using System.ComponentModel.DataAnnotations;

namespace MAFRagService.Startup.Configuration;

/// <summary>
/// NebulaGraph 应用层配置（区别于 Stubs.NebulaGraph.NebulaGraphOptions 的连接层配置）。
/// </summary>
public sealed class NebulaGraphAppOptions
{
    public const string SectionName = "NebulaGraph";

    [Required, MinLength(1)]
    public string Space { get; set; } = "rag_space";
}