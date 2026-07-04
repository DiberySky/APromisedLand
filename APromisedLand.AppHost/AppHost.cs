using Aspire.Hosting.DevTunnels;

var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder.AddKeycloak("Keycloak",8323)
    .WithDataVolume("keycloak-data")
    .WithOtlpExporter();

var postgres = builder.AddPostgres("Postgres", port: 8433)
    .WithDataVolume("postgres-data")
    .WithPgAdmin()
    .WithOtlpExporter();

var redis = builder.AddRedis("Redis")
    .WithDataVolume("redis-data", isReadOnly: false);

#region Ollama
// var ollama = builder.AddOllama("Ollama", port: 11434)
//     .WithDataVolume("ollama-data")
//     .WithGPUSupport()
//     .WithContainerRuntimeArgs("--gpus=all")
//     .WithLifetime(ContainerLifetime.Persistent)
//     .WithOtlpExporter();
// var embedding = ollama.AddModel("bge-large");
//
// var ollamaService = builder.AddProject<Projects.OllamaService>("Ollama-Service")
//     .WithReference(embedding)
//     .WaitFor(embedding)
//     .WithOtlpExporter();
#endregion

#region Elasticsearch
// // 1. 修改 Elasticsearch 配置，禁用安全功能以简化本地开发
// var elasticsearch = builder.AddElasticsearch("Elasticsearch")
//     .WithImage("elasticsearch:9.4.3")
//     .WithDockerfile("./Notes") 
//     .WithDataVolume("elasticsearch-data")
//     .WithEnvironment("xpack.security.enabled", "false") 
//     .WithHttpEndpoint(port: 9200, targetPort: 9200)
//     .WithEnvironment("http.cors.enabled", "true")
//     .WithEnvironment("http.cors.allow-origin", "http://localhost:8083")  // 对应你访问 Elasticvue 的地址
//     .WithEnvironment("http.cors.allow-headers", "X-Requested-With, Content-Type, Content-Length, Authorization")
//     .WithOtlpExporter();
//
// // 在 elasticsearch 定义之后添加
// var elasticvue = builder.AddContainer("Elasticvue", "cars10/elasticvue", "1.15.0")
//     .WithHttpEndpoint(port: 8083, targetPort: 8080, name: "elasticvue-http") // 宿主机端口 8081
//     .WithEnvironment("ELASTICSEARCH_HOSTS", "[\"http://Elasticsearch:9200\"]")    // 指向容器内的 Elasticsearch
//     .WithOtlpExporter()                                                       // 可选，启用遥测
//     .WaitFor(elasticsearch);                                                  // 等待 Elasticsearch 就绪
//
// // 2. 添加 Kibana 容器
// var kibana = builder.AddContainer("kibana", "kibana", "8.17.3") // 版本需与 Elasticsearch 主版本一致[reference:4]
//     .WithReference(elasticsearch) // 引用 Elasticsearch 容器
//     .WithEnvironment("ELASTICSEARCH_HOSTS", "http://Elasticsearch:9200") // 明确指定连接地址
//     .WithHttpEndpoint(port: 5601, targetPort: 5601) // 暴露 5601 端口
//     .WaitFor(elasticsearch); 
#endregion

#region Typesense
var typesenseApiKey = builder.AddParameter("typesense-api-key", secret: true);

var typesense = builder.AddContainer("typesense", "typesense/typesense", "30.2")
    .WithArgs("--data-dir", "/data", "--api-key", typesenseApiKey, "--enable-cors")
    .WithVolume("typesense-data", "/data")
    .WithHttpEndpoint(8108, 8108, name: "typesense");

var typeContainer = typesense.GetEndpoint("typesense");

var rabbitmq = builder.AddRabbitMQ("Messaging")
    .WithDataVolume("rabbitmq-data")
    .WithManagementPlugin(port: 15672);
#endregion

#region SeaweedFS FileTransService
// SeaweedFS Master
var seaweedMaster = builder.AddContainer("seaweedfs-master", "chrislusf/seaweedfs")
    .WithArgs("master", "-ip=seaweedfs-master", "-port=9333")
    .WithHttpEndpoint(name: "http", port: 9333, targetPort: 9333)
    .WithOtlpExporter();

// SeaweedFS Volume
var seaweedVolume = builder.AddContainer("seaweedfs-volume", "chrislusf/seaweedfs")
    .WithArgs("volume", "-mserver=seaweedfs-master:9333", "-port=8080", "-dir=/data")
    .WithHttpEndpoint(name: "http", port: 8080, targetPort: 8080) 
    .WithVolume("seaweedfs-volume-data", "/data")   // 使用 Docker 命名卷（推荐）
    .WithOtlpExporter()
    .WaitFor(seaweedMaster);

// SeaweedFS Filer
var seaweedFiler = builder.AddContainer("seaweedfs-filer", "chrislusf/seaweedfs")
    .WithArgs("filer", "-master=seaweedfs-master:9333")
    .WithHttpEndpoint(name: "http", port: 8888, targetPort: 8888)
    .WithOtlpExporter()
    .WaitFor(seaweedMaster)
    .WaitFor(seaweedVolume);

// API Service
var fileTransDb = postgres.AddDatabase("fileTransDb");

var fileTransService = builder.AddProject<Projects.FileTransService>("FileTrans-Service")
    .WithReference(fileTransDb)
    .WithEnvironment("SeaweedFS__BaseUrl", seaweedFiler.GetEndpoint("http"))
    .WithEnvironment("SeaweedFS__MasterUrl", seaweedMaster.GetEndpoint("http"))
    .WaitFor(fileTransDb)
    .WithOtlpExporter();
#endregion

// Question service
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

// var elasticsService = builder.AddProject<Projects.ElasticsearchService>("Elastic-Service")
//     .WithReference(rabbitmq)
//     .WithReference(elasticsearch)
//     .WaitFor(rabbitmq)
//     .WaitFor(elasticsearch);

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
        // yarp.AddRoute("/elastic/{**catch-all}", elasticsService);
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

#region Maui Blazor
var diberysky = builder.AddMauiProject("DiberySky", "../DiberySky/DiberySky.csproj");

diberysky.AddWindowsDevice()
    .WithReference(weatherapi)
    .WithReference(keycloak);  

// diberysky.AddAndroidEmulator()
//     .WithOtlpDevTunnel()
//     .WithReference(weatherapi, publicDevTunnel)
//     .WithReference(keycloak, publicDevTunnel);
#endregion

builder.Build().Run();