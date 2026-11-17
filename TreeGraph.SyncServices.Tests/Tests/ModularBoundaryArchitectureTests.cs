using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TreeGraph.SyncServices.Tests.Tests;

public sealed class ModularBoundaryArchitectureTests
{
    [Fact]
    public void SyncServices_does_not_reference_api_or_write_business_tables()
    {
        Assert.DoesNotContain(typeof(Program).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "TreeGraph.Api");

        var root = SolutionRoot();
        var projectReferences = XDocument.Load(Path.Combine(
                root, "TreeGraph.SyncServices", "TreeGraph.SyncServices.csproj"))
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .ToList();
        Assert.DoesNotContain(projectReferences,
            reference => reference?.EndsWith("TreeGraph.Api.csproj", StringComparison.OrdinalIgnoreCase) == true);

        var sourceFiles = Directory.EnumerateFiles(
            Path.Combine(root, "TreeGraph.SyncServices"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        foreach (var file in sourceFiles)
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotMatch(
                @"\.(SaveChanges|SaveChangesAsync|ExecuteDelete|ExecuteDeleteAsync|ExecuteUpdate|ExecuteUpdateAsync)\s*\(",
                source);
            Assert.DoesNotMatch(@"(?i)\b(INSERT\s+INTO|DELETE\s+FROM)\b", source);
        }
    }

    [Fact]
    public void Api_does_not_retain_projection_pipelines_or_their_registrations()
    {
        var apiRoot = Path.Combine(SolutionRoot(), "TreeGraph.Api");
        Assert.False(ContainsSourceFiles(Path.Combine(apiRoot, "GraphSyncSky")));
        Assert.False(ContainsSourceFiles(Path.Combine(apiRoot, "NodeEavSky", "SemanticSearch")));

        var program = File.ReadAllText(Path.Combine(apiRoot, "Program.cs"));
        Assert.DoesNotContain("AddGraphSync", program);
        Assert.DoesNotContain("AddSemanticSearch", program);
        Assert.DoesNotContain("AddHostedService<Vector", program);
    }

    private static bool ContainsSourceFiles(string path)
        => Directory.Exists(path)
            && Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).Any();

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "APromisedLand.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("未找到 APromisedLand.sln 所在仓库根目录");
    }
}
