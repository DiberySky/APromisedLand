var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("Keycloak", 8080)
    .WithDataVolume("keycloak-data")
    // .WithRealmImport("KeycloakRealm.json")
    .WithOtlpExporter();

var weatherapi = builder.AddProject<Projects.WeatherApi>("Weather-Api")
    .WithReference(keycloak)
    .WaitFor(keycloak);

var gateway = builder.AddYarp("Gateway")
    .WithHostPort(8088)
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/WeatherForecast/{**catch-all}", weatherapi);
    })
    .WithDeveloperCertificateTrust(true)
    .WithEnvironment("ASPNETCORE_URLS", "http://localhost:8443")
    .WithEndpoint(port: 8443, targetPort: 8443, scheme: "https", name: "gateway", isExternal: true);

builder.Build().Run();