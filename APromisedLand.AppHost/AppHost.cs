using APromisedLand.AppHost;
using APromisedLand.AppHost.Extensions;
using Aspire.Hosting.DevTunnels;

var builder = DistributedApplication.CreateBuilder(args);

var context = new AppHostContext();

builder.AddKeycloak(context); // Keycloak
builder.AddPostgres(context); // Postgres
builder.AddRedis(context); // Redis
builder.AddRabbitMq(context); // RabbitMQ
builder.AddTypesense(context); // Typesense
builder.AddOllama(context); // Ollama + embedding 
// builder.AddOllamaTesting(context); // Ollama Testing
// builder.AddSeaweedFs(context); // SeaweedFS Service
builder.AddElasticsearch(context); // Elasticsearch
builder.AddQuestionService(context); // QuestionService, WeatherApi
builder.AddQuestionElastic(context); // Elastic-Service
builder.AddQustionTypesense(context); // SearchService
// builder.AddGateway(context); // Yarp
// builder.AddDevTunnel(context); // DevTunnel
// builder.AddMauiApp(context); // Maui Blazor
// builder.AddElasticKibana(context); // Kibana
builder.AddElasticvue(context); // Elasticvue

builder.Build().Run();
