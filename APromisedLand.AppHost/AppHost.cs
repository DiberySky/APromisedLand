using APromisedLand.AppHost;
using APromisedLand.AppHost.Extensions;

var builder = DistributedApplication.CreateBuilder(args);

var context = new AppHostResourceContext();

TreeGraph();




// MafRagApi();
//MafWorkFlowService();

builder.Build().Run();
return;

void TreeGraph()
{
    builder.AddPostgres(context);
    builder.AddRedis(context);
    // builder.AddNebulaGraph(context);

    // ★ SeaweedFS：TreeGraph.FileStorageApi 的对象存储依赖
    //   (SeaweedMaster/Volume/Filer/S3 容器组，由 SeaweedFsExtension 声明)
    builder.AddSeaweedFs(context);

    // ★ EAV 动态类型 API(复用 TreeGraphDb,固定端口 5773)
    builder.AddTreeGraphEavApi(context);

    // ★ TreeGraph 文件存储 API(独立微服务,固定端口 5326,
    //   复用 FileMetadataDb + SeaweedS3,与 EavApi 平级)
    builder.AddTreeGraphFileStorageApi(context);

    // ★ TreeGraph 管理台 Blazor Server(固定端口 5783,引用 EavApi + FileStorageApi)
    builder.AddTreeGraphBlazor(context);
    builder.AddTreeGraphBlazorMaui(context);

    // ★ TreeGraph.Maui(MAUI+Blazor 混合模板起步项目,引用 EavApi 供后续服务发现)
    builder.AddTreeGraphMaui(context);
}

void MafRagApi()
{
    builder.AddVllm(context);
    builder.AddPostgres(context);
    builder.AddRedis(context);

    // ★ 不再需要 AddOllama —— Chat/Embedding 都走 vLLM
    // builder.AddOllama(context);

    // builder.AddFileStorageApi(context);
    builder.AddLiteGraph(context);
    builder.AddRerankerService(context);
    builder.AddMafVectorSearchApi(context);   // 向量搜索服务（需先于 MafSampleApi 声明）
    builder.AddMafSampleApi(context);

    // ★ 新增:泛型树形数据 API(复用 TreeDb,固定端口 5753)
    builder.AddTreeGraphApi(context);

    // ★ 新增:TreeGraph 前端 Blazor Server(固定端口 5763,调用 TreeGraphApi)
    builder.AddMafRagTreeGraph(context);
}

void MafSampleApiOllama()
{
    builder.AddPostgres(context);
    builder.AddRedis(context);
    builder.AddOllama(context);

    builder.AddFileStorageApi(context);

    builder.AddLiteGraph(context);

    // ★ 再声明 RerankerService（注入环境变量给 MAFWorkFlowApi）
    builder.AddRerankerService(context);
    
    builder.AddMafSampleApi(context);  
    
    // builder.AddBlazorWeb(context);
}


void MafWorkFlowService()
{
    builder.AddPostgres(context);
    builder.AddRedis(context);
    // builder.AddNebulaGraph(context);
    // builder.AddWeaviate(context);
    builder.AddSeaweedFs(context);
    builder.AddOllama(context);

    builder.AddFileStorageApi(context);

    // builder.AddNornicDb(context); 
    builder.AddLiteGraph(context);

    // ★ 再声明 RerankerService（注入环境变量给 MAFWorkFlowApi）
    builder.AddRerankerService(context);
    
    builder.AddMafWorkFlowApi(context);
    
    builder.AddBlazorWeb(context);
}

void MafRag()
{
    // builder.AddPostgres(context);
    // builder.AddRedis(context);
    // builder.AddNebulaGraph(context);
    // builder.AddWeaviate(context);
    // builder.AddSeaweedFs(context);
    // builder.AddOllama(context);
    //
    // builder.AddMafRagService(context); 
}

void MafChat()
{
// // Add Redis cache resource for session persistence
//     var cache = builder.AddRedis("cache");
// //.WithPersistence()
// //.WithDataVolume();
//
// // Add Ollama container with persistent volume for model caching
//     var ollama = builder.AddOllama("ollama")
//         .WithDataVolume();
//
// // Add a model to Ollama (default: llama3.2:1b)
//     var ollamaModel = ollama.AddModel("chat-model", "qwen2.5:7b"); //llama3.2:1b phi4-mini qwen2.5-7b
//
// // Task.Delay(1000).Wait();
//
// // Add the API project with Redis and Ollama references
//     var api = builder.AddProject<Projects.MafStatefulApi>("api")
//         .WithReference(cache)
//         .WaitFor(cache)
//         .WithReference(ollamaModel)
//         .WaitFor(ollamaModel);
//
// // Add the Web project and reference the API
//     var web = builder.AddProject<Projects.MafStatefulApi_Web>("web")
//         .WaitFor(cache)
//         .WaitFor(ollama)
//         .WaitFor(ollamaModel)
//         .WithReference(api)
//         .WaitFor(api);
//
//
// // Add the Client project and reference the API
//     var client = builder.AddProject<Projects.MafStatefulApi_Client>("client")
//         .WaitFor(cache)
//         .WaitFor(ollama)
//         .WaitFor(ollamaModel)
//         .WaitFor(web)
//         .WithReference(api)
//         .WaitFor(api);
}

void AddService()
{
//     // 添加 Qwen3-ASR 容器，暴露 ASR 服务端口（假设是 8000）
//     // 使用 FunASR 官方镜像（CPU 版本，若需 GPU 加速可换 funasr-runtime-sdk-gpu:latest）
//     // var asrService = builder.AddDockerfile("qwen3-asr", "../FunAsrService")
//     //     .WithHttpEndpoint(port: 8000, targetPort: 8000, name: "asr-http")
//     //     .WithBindMount(@"D:\DiberyModelSky\Qwen--Qwen3-ASR-1.7B", "/app/model")
//     //     .WithBuildArg("BUILDKIT_PROGRESS", "plain");  // 显示完整构建日志
//
//     var compose = builder.AddDockerComposeEnvironment("production")
//         .WithDashboard(dashboardOptions => dashboardOptions.WithHostPort(8090));
//
//     // var context = new AppHostResourceContext();
//
//     // builder.AddKeycloak(context); // Keycloak
//     builder.AddPostgres(context); // Postgres
//     builder.AddRedis(context); // Redis
//     builder.AddOllama(context); // Ollama + embedding 
//     builder.AddNebulaStudio(context); // NebulaStudio
//
//     builder.AddMafStateful(context); // MafStateful
// // builder.AddMafAi(context); // MafAi
//
//     builder.AddDiberyTreeService(context);
//     builder.AddDiberyMauiSky(context); // Dibery Maui Blazor
//
//     // builder.AddFoundry(context); // Foundry Local
//
//     // builder.AddNebulaGraph(context); // NebulaGraph
//     // builder.AddNebulaGraphApiService(context); // NebulaGraphApiService
//     // builder.AddNebulaGraphFastApiService(context); // AddNebulaGraphFastApiService
//     // builder.AddNebulaApiProxyService(context); // NebulaApiProxyService
//
//     // builder.AddWeatherService(context); // WeatherApi
//
//
//     // builder.AddYarp(context); // Yarp
//     // builder.AddDevTunnel(context); // DevTunnel
//
//     // builder.AddMauiApp(context); // Maui Blazor
//
//     // builder.AddSemanticSearch(context);
//     // builder.AddNats(context); // Nats
//     // builder.AddTypesense(context); // Typesense
//     // builder.AddElasticsearch(context); // Elasticsearch
//     // builder.AddSeaweedFs(context); // SeaweedFS Service
//     // builder.AddQuestionService(context); // QuestionService, WeatherApi
//     // builder.AddQuestionElastic(context); // Elastic-Service
//     // builder.AddQustionTypesense(context); // SearchService
//     // builder.AddElasticKibana(context); // Kibana
//     // builder.AddRabbitMq(context); // RabbitMQ
//     // builder.AddBlazorWasm(context); // Blazor, BlazorGateway, BlazorWasmAppResource
}