using Aspire.Hosting.DevTunnels;

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("Keycloak",8080)
    .WithDataVolume("keycloak-data")
    .WithOtlpExporter();

var postgres = builder.AddPostgres("Postgres", port: 8433)
    .WithDataVolume("postgres-data")
    .WithPgAdmin()
    .WithOtlpExporter();

var questionDb = postgres.AddDatabase("questionDb");

var questionService = builder.AddProject<Projects.QuestionService>("Question-Service")
    .WithReference(keycloak)
    .WithReference(questionDb)
    .WaitFor(keycloak)
    .WaitFor(questionDb);

var weatherapi = builder.AddProject<Projects.WeatherApi>("Weather-Api")
    .WithReference(keycloak)
    .WaitFor(keycloak);

var gateway = builder.AddYarp("Yarp")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/WeatherForecast/{**catch-all}", weatherapi);
        yarp.AddRoute("/Question/{**catch-all}", questionService);
        yarp.AddRoute("/tags/{**catch-all}", questionService);
    })
    .WithHttpEndpoint(port: 8090, targetPort: 8090, name: "http")
    .WithOtlpExporter();

builder.AddDevTunnel("DevTunnel-public")
    .WithAnonymousAccess()
    .WithEnvironment("TUNNEL_ACCESS", "anonymous")
    .WithReference(keycloak.GetEndpoint("http"), new DevTunnelPortOptions
    {
         Protocol = "https"
    })
    .WithReference(gateway.GetEndpoint("http"));  

var diberysky = builder.AddMauiProject("DiberySky", "../DiberySky/DiberySky.csproj");

diberysky.AddWindowsDevice()
    .WithReference(weatherapi)
    .WithReference(keycloak);  

// diberysky.AddAndroidEmulator()
//     .WithOtlpDevTunnel()
//     .WithReference(weatherapi, publicDevTunnel)
//     .WithReference(keycloak, publicDevTunnel);

builder.Build().Run();