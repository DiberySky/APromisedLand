using Aspire.Hosting.DevTunnels;
using Aspire.Hosting.Maui;
using Aspire.Hosting.Python;

namespace APromisedLand.AppHost;

public class AppHostContext
{
    // Keycloak
    public IResourceBuilder<KeycloakResource>? Keycloak { get; set; }

    // Database & RabbitMQ
    public IResourceBuilder<PostgresServerResource>? Postgres { get; set; }
    public IResourceBuilder<PostgresDatabaseResource>? QuestionDb { get; set; }
    public IResourceBuilder<PostgresDatabaseResource>? FileTransDb { get; set; }
    public IResourceBuilder<PostgresDatabaseResource>? TreeDb { get; set; }
    public IResourceBuilder<RedisResource>? Redis { get; set; }
    public IResourceBuilder<RabbitMQServerResource>? RabbitMq { get; set; }
    public IResourceBuilder<NatsServerResource>? Nats { get; set; }

    // Search (Typesense)
    public IResourceBuilder<ContainerResource>? Typesense { get; set; }

    public EndpointReference? TypesenseEndpoint { get; set; }

    // public IResourceBuilder<ParameterResource>? TypesenseApiKey { get; set; }
    public string? TypesenseApiKey { get; set; }
    
    // NebulaGraph
    public IResourceBuilder<ContainerResource>? NebulaGraph { get; set; } // NebulaGraph 容器
    public IResourceBuilder<ContainerResource>? NebulaConsole { get; set; } // NebulaGraph 容器
    public EndpointReference? NebulaGraphEndpoint { get; set; } // NebulaGraph 服务端口
    public IResourceBuilder<ContainerResource>? NebulaStudio { get; set; } // NebulaStudio 容器
    public IResourceBuilder<ProjectResource>? NebulaApiProxy { get; set; }
    
    // ← 新增：Python FastAPI 服务引用
    public IResourceBuilder<UvicornAppResource>? NebulaGraphFastApi { get; set; }
    public EndpointReference? NebulaGraphFastApiEndpoint { get; set; }
    
    // Storage (SeaweedFS)
    public IResourceBuilder<ContainerResource>? SeaweedMaster { get; set; }
    public IResourceBuilder<ContainerResource>? SeaweedVolume { get; set; }
    public IResourceBuilder<ContainerResource>? SeaweedFiler { get; set; }

    // AI (Ollama)
    public IResourceBuilder<OllamaResource>? Ollama { get; set; }
    public IResourceBuilder<OllamaModelResource>? Embedding { get; set; }

    // Elasticsearch
    public IResourceBuilder<ElasticsearchResource>? Elasticsearch { get; set; }
    public IResourceBuilder<ContainerResource>? Elasticvue { get; set; }
    public IResourceBuilder<ContainerResource>? Kibana { get; set; }

    // Business Services
    public IResourceBuilder<ProjectResource>? DiberyTreeService { get; set; }
    public IResourceBuilder<ProjectResource>? QuestionService { get; set; }
    public IResourceBuilder<ProjectResource>? WeatherApi { get; set; }
    public IResourceBuilder<ProjectResource>? TypesenseService { get; set; }
    public IResourceBuilder<ProjectResource>? FileTransService { get; set; }
    public IResourceBuilder<ProjectResource>? ElasticService { get; set; }
    public IResourceBuilder<ProjectResource>? OllamaService { get; set; }

    // Gateway
    public IResourceBuilder<Aspire.Hosting.Yarp.YarpResource>? YarpGateway { get; set; }
// IResourceBuilder<DevTunnelResource>

    public IResourceBuilder<DevTunnelResource>? PublicDevTunnel { get; set; }

    // Maui
    public IResourceBuilder<MauiProjectResource>? DiberySky { get; set; }
    public IResourceBuilder<MauiProjectResource>? DiberyMauiSky { get; set; }
}