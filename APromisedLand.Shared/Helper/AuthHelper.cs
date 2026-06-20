namespace APromisedLand.Shared.Helper;

public static class AuthHelper
{
    public const string Realm = "apromisedland";

    public const string ClientId = "diberysky";

    public const string KeyCloakPort = "8088";

    public const string YarpkPort = "8001";

    public const string Authority = $"https://localhost:{KeyCloakPort}/realms/{Realm}";

    public const string KeyCloakHttpsHostsAddress = $"https://localhost:{KeyCloakPort}";

    public const string YarpHttpHostAddress = $"http://localhost:{YarpkPort}";
}