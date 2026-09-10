using System.Diagnostics;

namespace MAFRagService.Startup.Diagnostics;

public static class MAFRagActivity
{
    public const string Name = "MAFRagService";
    public static readonly ActivitySource Source = new(Name);
}