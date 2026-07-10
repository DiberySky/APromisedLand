using APromisedLand.AppHost;
using APromisedLand.AppHost.Extensions;
using Aspire.Hosting.DevTunnels;

var builder = DistributedApplication.CreateBuilder(args);

var compose = builder.AddDockerComposeEnvironment("production")
    .WithDashboard(dashboardOptions => dashboardOptions.WithHostPort(8090));

var context = new AppHostContext();

// builder.AddKeycloak(context); // Keycloak
// builder.AddPostgres(context); // Postgres
// builder.AddRedis(context); // Redis
//     // builder.AddRabbitMq(context); // RabbitMQ
// builder.AddNats(context); // Nats
// builder.AddTypesense(context); // Typesense
// builder.AddOllama(context); // Ollama + embedding 
//     // builder.AddSeaweedFs(context); // SeaweedFS Service
// builder.AddElasticsearch(context); // Elasticsearch
// builder.AddQuestionService(context); // QuestionService, WeatherApi
//     // builder.AddQuestionElastic(context); // Elastic-Service
//     // builder.AddQustionTypesense(context); // SearchService
//     // builder.AddYarp(context); // Yarp
//     // builder.AddDevTunnel(context); // DevTunnel
//     // builder.AddMauiApp(context); // Maui Blazor
//     // builder.AddElasticKibana(context); // Kibana
// builder.AddSemanticSearch(context);
builder.AddDiberyTreeService(context);

builder.Build().Run();
