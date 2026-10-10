#if DEBUG
using System.Reflection;

namespace TreeGraph.Blazor.Shared.Common.Dev;

/// <summary>
/// DEBUG 专用：把弹窗内容组件类型还原为 .razor 源文件路径。
/// 策略：
///   1. 读 Razor SDK Debug 构建生成的 RazorSourceChecksumAttribute（含源文件路径）；
///   2. 回退：从 AppContext.BaseDirectory 向上找 .sln，再按「类型名.razor」搜索（Razor 约定类名=文件名）。
/// 结果带缓存；仅 Dev 环境使用，Release 不编译。
/// </summary>
public static class DevSourceFileLocator
{
    private static readonly Dictionary<string, string?> FileIndexCache = new(StringComparer.OrdinalIgnoreCase);
    private static string? _solutionRoot;
    private static bool _solutionRootSearched;

    /// <summary>返回源文件完整路径；找不到返回 null。</summary>
    public static string? Locate(Type componentType)
    {
        var fromAttribute = LocateFromSourceChecksum(componentType);
        if (fromAttribute is not null)
        {
            return fromAttribute;
        }

        return LocateByFileName(componentType);
    }

    /// <summary>显示用文件名：剥离泛型 arity（TreeNodeActionsDialog`1 → TreeNodeActionsDialog.razor）。</summary>
    public static string DisplayFileName(Type componentType)
        => componentType.Name.Split('`')[0] + ".razor";

    private static string? LocateByFileName(Type type)
    {
        var fileName = DisplayFileName(type);

        lock (FileIndexCache)
        {
            if (FileIndexCache.TryGetValue(fileName, out var cached))
            {
                return cached;
            }

            string? found = null;
            var root = FindSolutionRoot();
            if (root is not null)
            {
                var candidates = Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                             && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    .ToList();

                // 同名文件多处存在时（如 APromisedLand.Razor 与 TreeGraph.Blazor.Shared 各有
                // TreeNodeActionsDialog.razor）：按命名空间尾段与目录段匹配度择优。
                var nsParts = (type.Namespace ?? "").Split('.',
                    StringSplitOptions.RemoveEmptyEntries);
                found = candidates
                    .OrderByDescending(f => ScoreNamespaceMatch(f, nsParts))
                    .FirstOrDefault();
            }

            FileIndexCache[fileName] = found;
            return found;
        }
    }

    private static int ScoreNamespaceMatch(string path, string[] nsParts)
    {
        var dirParts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var score = 0;
        for (var i = 1; i <= nsParts.Length && i <= dirParts.Length; i++)
        {
            if (string.Equals(nsParts[^i], dirParts[^(i + 1)], StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }
            else
            {
                break;
            }
        }

        return score;
    }

    private static string? LocateFromSourceChecksum(Type type)
    {
        foreach (var attr in type.GetCustomAttributesData())
        {
            if (attr.AttributeType.FullName != "Microsoft.AspNetCore.Razor.Hosting.RazorSourceChecksumAttribute")
            {
                continue;
            }

            // 构造参数：(string algorithm, string checksum, string filePath)
            if (attr.ConstructorArguments.Count < 3 ||
                attr.ConstructorArguments[2].Value is not string path ||
                string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            // 绝对路径直接可用
            if (Path.IsPathRooted(path) && File.Exists(path))
            {
                return path;
            }

            // 项目相对路径（如 /Components/Layout/X.razor）：拼到解决方案根目录下逐项目探测
            var root = FindSolutionRoot();
            if (root is null)
            {
                return null;
            }

            var trimmed = path.TrimStart('/', '\\');
            var hit = Directory.EnumerateFiles(root, Path.GetFileName(trimmed), SearchOption.AllDirectories)
                .FirstOrDefault(f => f.Replace('\\', '/').EndsWith(trimmed.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
            return hit;
        }

        return null;
    }

    private static string? FindSolutionRoot()
    {
        if (_solutionRootSearched)
        {
            return _solutionRoot;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.EnumerateFiles("*.sln").Any())
            {
                _solutionRoot = dir.FullName;
                break;
            }

            dir = dir.Parent;
        }

        _solutionRootSearched = true;
        return _solutionRoot;
    }
}
#endif

