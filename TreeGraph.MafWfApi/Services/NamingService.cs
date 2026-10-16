using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using TreeGraph.MafWfApi.Models;

namespace TreeGraph.MafWfApi.Services;

/// <summary>
/// 中文 → 英文变量名生成。
///
/// 流程：
///   ① LLM 把中文翻译为"空格分隔的英文单词"（只做翻译，不做大小写/缩写处理）
///   ② 代码侧规范化：缩写映射 → 驼峰/字母数字边界拆分 → 统一大小写
///   ③ 拼接生成 snake_case 与 PascalCase
///   ④ 检测 C# 关键字 / BCL 类型名冲突
///
/// 这样把"翻译"和"命名规则"解耦，即使 LLM 有抖动，命名规则也稳定可预测。
/// </summary>
public sealed class NamingService
{
    // ── 通用缩写：整词命中时映射为"首字母大写+其余小写"形式 ──
    private static readonly Dictionary<string, string> AbbrevMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["ID"]    = "Id",   ["URL"]  = "Url",  ["URI"]  = "Uri",
            ["API"]   = "Api",  ["HTTP"] = "Http", ["HTTPS"]= "Https",
            ["XML"]   = "Xml",  ["JSON"] = "Json", ["SQL"]  = "Sql",
            ["UI"]    = "Ui",   ["DB"]   = "Db",   ["UUID"] = "Uuid",
            ["GUID"]  = "Guid", ["HTML"] = "Html", ["CSS"]  = "Css",
            ["JWT"]   = "Jwt",  ["TCP"]  = "Tcp",  ["UDP"]  = "Udp",
            ["DNS"]   = "Dns",  ["SSH"]  = "Ssh",  ["GPU"]  = "Gpu",
            ["CPU"]   = "Cpu",  ["OS"]   = "Os",   ["SDK"]  = "Sdk",
            ["CLI"]   = "Cli",  ["RPC"]  = "Rpc",  ["TLS"]  = "Tls",
            ["SSL"]   = "Ssl",  ["FTP"]  = "Ftp",  ["SMTP"] = "Smtp",
        };

    // ── 网络协议缩写：保留原样（避免 IPv6 → Ipv6）──
    private static readonly HashSet<string> PreserveAsIs =
        new(StringComparer.OrdinalIgnoreCase) { "IPv4", "IPv6" };

    // ── 驼峰边界：小写/数字 后跟 大写 ──
    private static readonly Regex CamelBoundary =
        new(@"(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);

    // ── 字母↔数字 边界：Level3 → Level | 3 ──
    private static readonly Regex LetterDigitBoundary =
        new(@"(?<=[A-Za-z])(?=[0-9])|(?<=[0-9])(?=[A-Za-z])",
            RegexOptions.Compiled);

    // ── C# 关键字（snake_case 命中时提示需要 @ 前缀）──
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract","as","base","bool","break","byte","case","catch","char","checked",
        "class","const","continue","decimal","default","delegate","do","double",
        "else","enum","event","explicit","extern","false","finally","fixed","float",
        "for","foreach","goto","if","implicit","in","int","interface","internal",
        "is","lock","long","namespace","new","null","object","operator","out",
        "override","params","private","protected","public","readonly","ref","return",
        "sbyte","sealed","short","sizeof","stackalloc","static","string","struct",
        "switch","this","throw","true","try","typeof","uint","ulong","unchecked",
        "unsafe","ushort","using","virtual","void","volatile","while",
    };

    // ── BCL 常见类型名（PascalCase 恰好同名时轻警告）──
    private static readonly HashSet<string> BclTypeNames = new(StringComparer.Ordinal)
    {
        "String","Object","Task","Type","Array","Enum","Delegate","Attribute",
        "Exception","Action","Func","Tuple","Nullable","ValueTask",
    };

    private const string SystemPrompt = """
        你是中英技术术语翻译器，把中文短语翻译为英文标识符片段。

        严格规则：
        1. 只输出英文单词，用单个空格分隔。
        2. 每个单词首字母大写，其余字母小写；输入中已有的英文单词保留原样。
        3. 数字单独成词，例如：Level 3 Menu。
        4. 常见缩写统一写为：Id、Url、Api、Http、Json、Xml、Sql、Ui、Db、Guid、Dns、Ssh。
        5. 网络协议缩写保留原样：IPv4、IPv6。
        6. 不要输出标点、解释、引号、代码块、换行。

        示例：
        用户ID → User Id
        三级菜单 → Level 3 Menu
        IPv6地址 → IPv6 Address
        用户Profile → User Profile
        DNS服务器 → DNS Server
        图中的邻居节点 → Graph Neighbor Node
        订单支付状态 → Order Payment Status

        只输出翻译结果，不要任何多余内容。
        """;

    private readonly IChatClient _chatClient;
    private readonly ILogger<NamingService> _logger;

    public NamingService(IChatClient chatClient, ILogger<NamingService> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    // ═════════════════════════════════════════════════════════
    // 入口
    // ═════════════════════════════════════════════════════════
    public async Task<NamingResult> GenerateAsync(string input, CancellationToken ct)
    {
        var trimmed = input.Trim();

        // ① LLM 翻译：中文 → 空格分隔英文（null = 翻译失败）
        var english = await TranslateAsync(trimmed, ct);

        // ② 拆词 + 缩写/数字/中英混排规范化（失败时退回原输入，至少不抛异常）
        var tokens = NormalizeTokens(english ?? trimmed);

        // ③ 生成两种命名
        var pascal = string.Concat(tokens);
        var snake  = ToSnakeCase(tokens);

        // ④ 冲突检测
        var warnings = DetectWarnings(snake, pascal);

        // 翻译失败：结果可能含中文，必须显式告知调用方
        if (english is null)
        {
            warnings.Insert(0,
                "LLM 翻译失败（返回空或无效内容），以下结果直接由原始输入生成，可能包含中文，仅供参考。");
        }

        _logger.LogInformation(
            "Naming: '{Src}' → {En} | snake={S}, pascal={P}, warnings={W}",
            trimmed, english ?? "(failed)", snake, pascal, warnings.Count);

        return new NamingResult(
            Source: trimmed,
            SnakeCase: snake,
            PascalCase: pascal,
            Warnings: warnings);
    }

    // ═════════════════════════════════════════════════════════
    // ① LLM 翻译（只做翻译，不做命名规则）
    //    返回 null 表示翻译失败（空/清洗后无内容）
    // ═════════════════════════════════════════════════════════
    private async Task<string?> TranslateAsync(string input, CancellationToken ct)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, input),
        };

        var options = new ChatOptions
        {
            Temperature = 0f,
            MaxOutputTokens = 64,
        };

        var response = await _chatClient.GetResponseAsync(messages, options, ct);
        var text = (response.Text ?? string.Empty).Trim();

        // 容错 1：剥掉 Markdown 代码块围栏
        if (text.Contains("```"))
        {
            text = string.Join(" ",
                text.Split('\n')
                    .Where(l => !l.TrimStart().StartsWith("```"))
                    .ToArray());
        }

        // 容错 2：去引号与其它标点
        text = text.Trim('`', '"', '\'', ' ', '\n', '\r');

        // 容错 3：只保留字母数字与空格
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');

        var cleaned = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();

        if (string.IsNullOrEmpty(cleaned))
        {
            _logger.LogWarning("LLM 返回空翻译，源输入：'{Src}'", input);
            return null;
        }

        return cleaned;
    }

    // ═════════════════════════════════════════════════════════
    // ② 拆词 + 缩写规范化
    // ═════════════════════════════════════════════════════════
    private static List<string> NormalizeTokens(string english)
    {
        var result = new List<string>();
        var raw = english.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in raw)
        {
            // 白名单原样保留（IPv4 / IPv6）
            if (PreserveAsIs.Contains(token))
            {
                result.Add(token);
                continue;
            }

            // 先按 驼峰 + 字母数字 边界拆成原子片段
            foreach (var part in SplitIntoParts(token))
            {
                // 整片段命中通用缩写 → 用映射值
                if (AbbrevMap.TryGetValue(part, out var mapped))
                {
                    result.Add(mapped);
                    continue;
                }

                // 其它情况 → 首字母大写
                result.Add(Capitalize(part));
            }
        }

        return result;
    }

    /// <summary>
    /// 把一段英文切成原子 token：
    /// 先按驼峰边界拆，再按字母↔数字边界拆。
    ///   "Level3Menu" → ["Level","3","Menu"]
    ///   "UserID"     → ["User","ID"]
    ///   "UserId"     → ["User","Id"]
    ///   "DNS"        → ["DNS"]
    /// </summary>
    private static IEnumerable<string> SplitIntoParts(string s)
    {
        foreach (var p in CamelBoundary.Split(s))
        {
            if (p.Length == 0) continue;

            foreach (var seg in LetterDigitBoundary.Split(p))
                if (seg.Length > 0) yield return seg;
        }
    }

    /// <summary>
    /// 单个原子片段的最终形态：
    ///   "user"  → "User"
    ///   "User"  → "User"   （已首字母大写，保留）
    ///   "DNS"   → "Dns"    （全大写视为缩写，转首字母大写）
    ///   "3"     → "3"      （纯数字原样）
    /// </summary>
    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.All(char.IsDigit)) return s;

        // 全大写（无小写）：视为未知缩写 → 首字母大写其余小写
        if (!s.Any(char.IsLower))
            return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

        // 已含非首字母大写（驼峰内部）：保留原样
        if (s.Skip(1).Any(char.IsUpper)) return s;

        // 首字母大写
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    // ═════════════════════════════════════════════════════════
    // ③ snake_case（tokens 已是原子片段，直接 lower + join）
    // ═════════════════════════════════════════════════════════
    private static string ToSnakeCase(List<string> tokens)
        => string.Join("_", tokens.Select(t => t.ToLowerInvariant()));

    // ═════════════════════════════════════════════════════════
    // ④ 冲突检测
    // ═════════════════════════════════════════════════════════
    private static List<string> DetectWarnings(string snake, string pascal)
    {
        var warnings = new List<string>();

        // snake_case 的任一单词命中 C# 关键字 → 变量名需 @ 前缀
        foreach (var w in snake.Split('_', StringSplitOptions.RemoveEmptyEntries))
        {
            if (CSharpKeywords.Contains(w))
            {
                warnings.Add(
                    $"snake_case 结果中的 '{w}' 是 C# 关键字，" +
                    $"作为变量名需加 @ 前缀：@{snake}。建议改用其它名称。");
                break;
            }
        }

        // PascalCase 恰好与 BCL 常见类型同名 → 属性名合法但易混淆
        if (BclTypeNames.Contains(pascal))
        {
            warnings.Add(
                $"PascalCase 结果 '{pascal}' 与 BCL 常见类型名同名，" +
                "作为属性名合法，但与 System 命名空间下的类型易混淆。");
        }

        return warnings;
    }
}
