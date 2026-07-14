namespace APromisedLand.Shared.Services.Solution;

public partial class SolutionService
{
    public static string Realm { get; set; } = "apromisedland";
    public static string ClientId { get; set; } = "diberysky";
    
    // public static string KeyCloakPort { get; set; } = "8080";
    // public static string Authority { get; set; } = $"https://localhost:{KeyCloakPort}/realms/{Realm}";

    public static string KeyCloakHttpsBaseUrl => "https://6fjddmjf-8323.jpe1.devtunnels.ms";
    
    public static string YarpHostBaseUrl { get; set; } = "https://6fjddmjf-8919.jpe1.devtunnels.ms";
}