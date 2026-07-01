using Aspire.Hosting.DevTunnels;

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("Keycloak",8080)
    .WithDataVolume("keycloak-data")
    .WithOtlpExporter();

var postgres = builder.AddPostgres("Postgres", port: 8433)
    .WithDataVolume("postgres-data")
    .WithPgAdmin()
    .WithOtlpExporter();

var redis = builder.AddRedis("Redis")
    .WithDataVolume("redis-data", isReadOnly: false);

var typesenseApiKey = builder.AddParameter("typesense-api-key", secret: true);

var typesense = builder.AddContainer("typesense", "typesense/typesense", "30.2")
    .WithArgs("--data-dir", "/data", "--api-key", typesenseApiKey, "--enable-cors")
    .WithVolume("typesense-data", "/data")
    .WithHttpEndpoint(8108, 8108, name: "typesense");

var typeContainer = typesense.GetEndpoint("typesense");

var rabbitmq = builder.AddRabbitMQ("Messaging")
    .WithDataVolume("rabbitmq-data")
    .WithManagementPlugin(port: 15672);

var questionDb = postgres.AddDatabase("questionDb");

var questionService = builder.AddProject<Projects.QuestionService>("Question-Service")
    .WithReference(keycloak)
    .WithReference(questionDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    .WaitFor(keycloak)
    .WaitFor(questionDb)
    .WaitFor(rabbitmq)
    .WaitFor(redis);

var searchService = builder.AddProject<Projects.SearchService>("Search-Service")
    .WithEnvironment("typesense-api-key", typesenseApiKey)
    .WithReference(typeContainer)
    .WithReference(rabbitmq)
    .WaitFor(typesense)
    .WaitFor(rabbitmq);

var weatherapi = builder.AddProject<Projects.WeatherApi>("Weather-Api")
    .WithReference(keycloak)
    .WaitFor(keycloak);

var gateway = builder.AddYarp("Yarp")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/WeatherForecast/{**catch-all}", weatherapi);
        yarp.AddRoute("/Questions/{**catch-all}", questionService);
        yarp.AddRoute("/tags/{**catch-all}", questionService);
        yarp.AddRoute("/search-mini/{**catch-all}", searchService);
        yarp.AddRoute("/typesense/{**catch-all}", searchService);
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