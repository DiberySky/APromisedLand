using APromisedLand.AppHost;
using APromisedLand.AppHost.Extensions;
using Aspire.Hosting.DevTunnels;

var builder = DistributedApplication.CreateBuilder(args);

// 添加 Qwen3-ASR 容器，暴露 ASR 服务端口（假设是 8000）
// 使用 FunASR 官方镜像（CPU 版本，若需 GPU 加速可换 funasr-runtime-sdk-gpu:latest）
// var asrService = builder.AddDockerfile("qwen3-asr", "../FunAsrService")
//     .WithHttpEndpoint(port: 8000, targetPort: 8000, name: "asr-http")
//     .WithBindMount(@"D:\DiberyModelSky\Qwen--Qwen3-ASR-1.7B", "/app/model")
//     .WithBuildArg("BUILDKIT_PROGRESS", "plain");  // 显示完整构建日志

var compose = builder.AddDockerComposeEnvironment("production")
    .WithDashboard(dashboardOptions => dashboardOptions.WithHostPort(8090));

var context = new AppHostContext();

// builder.AddKeycloak(context); // Keycloak
builder.AddPostgres(context); // Postgres
builder.AddRedis(context); // Redis

// builder.AddWeatherService(context); // WeatherApi

builder.AddDiberyTreeService(context);

// builder.AddYarp(context); // Yarp
// builder.AddDevTunnel(context); // DevTunnel

// builder.AddMauiApp(context); // Maui Blazor
builder.AddDiberyMauiSky(context); // Dibery Maui Blazor

// builder.AddSemanticSearch(context);
// builder.AddNats(context); // Nats
// builder.AddTypesense(context); // Typesense
// builder.AddOllama(context); // Ollama + embedding 
// builder.AddElasticsearch(context); // Elasticsearch
// builder.AddSeaweedFs(context); // SeaweedFS Service
// builder.AddQuestionService(context); // QuestionService, WeatherApi
// builder.AddQuestionElastic(context); // Elastic-Service
// builder.AddQustionTypesense(context); // SearchService
// builder.AddElasticKibana(context); // Kibana
// builder.AddRabbitMq(context); // RabbitMQ
// builder.AddBlazorWasm(context); // Blazor, BlazorGateway, BlazorWasmAppResource

builder.Build().Run();
