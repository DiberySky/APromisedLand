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

// 1. 修改 Elasticsearch 配置，禁用安全功能以简化本地开发
var elasticsearch = builder.AddElasticsearch("Elasticsearch")
    .WithDataVolume("elasticsearch-data")
    .WithEnvironment("xpack.security.enabled", "false") 
    .WithHttpEndpoint(port: 9200, targetPort: 9200)
    .WithEnvironment("http.cors.enabled", "true")
    .WithEnvironment("http.cors.allow-origin", "http://localhost:8083")  // 对应你访问 Elasticvue 的地址
    .WithEnvironment("http.cors.allow-headers", "X-Requested-With, Content-Type, Content-Length, Authorization")
    .WithOtlpExporter();


// 在 elasticsearch 定义之后添加
var elasticvue = builder.AddContainer("Elasticvue", "cars10/elasticvue", "1.15.0")
    .WithHttpEndpoint(port: 8083, targetPort: 8080, name: "elasticvue-http") // 宿主机端口 8081
    .WithEnvironment("ELASTICSEARCH_HOSTS", "[\"http://Elasticsearch:9200\"]")    // 指向容器内的 Elasticsearch
    .WithOtlpExporter()                                                       // 可选，启用遥测
    .WaitFor(elasticsearch);                                                  // 等待 Elasticsearch 就绪

// 2. 添加 Kibana 容器
var kibana = builder.AddContainer("kibana", "kibana", "8.17.3") // 版本需与 Elasticsearch 主版本一致[reference:4]
    .WithReference(elasticsearch) // 引用 Elasticsearch 容器
    .WithEnvironment("ELASTICSEARCH_HOSTS", "http://Elasticsearch:9200") // 明确指定连接地址
    .WithHttpEndpoint(port: 5601, targetPort: 5601) // 暴露 5601 端口
    .WaitFor(elasticsearch); 

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

var typesenseService = builder.AddProject<Projects.SearchService>("Typesense-Service")
    .WithEnvironment("typesense-api-key", typesenseApiKey)
    .WithReference(typeContainer)
    .WithReference(rabbitmq)
    .WaitFor(typesense)
    .WaitFor(rabbitmq);

var elasticsService = builder.AddProject<Projects.ElasticsearchService>("Elastic-Service")
    .WithReference(rabbitmq)
    .WithReference(elasticsearch)
    .WaitFor(rabbitmq)
    .WaitFor(elasticsearch);

var weatherapi = builder.AddProject<Projects.WeatherApi>("Weather-Api")
    .WithReference(keycloak)
    .WaitFor(keycloak);

var gateway = builder.AddYarp("Yarp")
    .WithConfiguration(yarp =>
    {
        yarp.AddRoute("/WeatherForecast/{**catch-all}", weatherapi);
        yarp.AddRoute("/Questions/{**catch-all}", questionService);
        yarp.AddRoute("/tags/{**catch-all}", questionService);
        yarp.AddRoute("/search-mini/{**catch-all}", typesenseService);
        yarp.AddRoute("/typesense/{**catch-all}", typesenseService);
        yarp.AddRoute("/elastic/{**catch-all}", elasticsService);
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