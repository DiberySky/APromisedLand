namespace APromisedLand.Shared.Services;

public partial class ProjectService
{
    public static string Realm { get; set; } = "apromisedland";
    public static string ClientId { get; set; } = "diberysky";
    
    private static string KeyCloakPort { get; set; } = "8080";
    public static string Authority { get; set; } = $"https://localhost:{KeyCloakPort}/realms/{Realm}";

    public static string KeyCloakHttpsBaseUrl => "https://6fjddmjf-8080.jpe1.devtunnels.ms";
    
    public static string YarpHostBaseUrl { get; set; } = "https://6fjddmjf-8090.jpe1.devtunnels.ms";
}