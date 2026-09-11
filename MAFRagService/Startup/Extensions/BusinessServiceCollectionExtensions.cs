using MAFRagService.Connectors;
using MAFRagService.Memory;
using MAFRagService.Services;
using MAFRagService.Services.TextChunking;
using MAFRagService.Services.Weaviate;
using MAFRagService.Startup.Configuration;
using MAFRagService.Stubs.MAF;
using MAFRagService.Stubs.NebulaGraph;
using Microsoft.Extensions.Options;

namespace MAFRagService.Startup.Extensions;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddRagBusinessServices(
        this IServiceCollection services,
        FeatureFlags features)
    {
        // ---------- Rag 核心 ----------
        if (features.Rag)
        {
            services.AddScoped<DocumentMetadataService>();
            services.AddScoped<DocumentStorageService>();
            services.AddScoped<DocumentAuditService>();
            services.AddScoped<RagService>();
            services.AddScoped<VersionManager>();

            services.AddScoped<ITextChunker, TextChunker>();
            services.AddScoped<IWeaviateChunkWriter, WeaviateChunkWriter>();
            services.AddScoped<IOllamaEmbeddingClient>(sp =>
            {
                var http = sp.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(HttpClientNames.Ollama);
                return new OllamaEmbeddingClient(
                    http,
                    sp.GetRequiredService<IOptions<OllamaOptions>>());
            });
        }

        // ---------- 索引器（依赖 Rag）----------
        if (features.Indexing && features.Rag)
        {
            services.AddScoped<EventStoreService>();
            services.AddScoped<IndexTaskService>();
            services.AddScoped<IncrementalIndexer>();

            // IndexerCapabilities：Graph 关闭时 Nebula 为 null
            services.AddScoped(sp =>
            {
                NebulaGraphExecutor? nebula = null;
                if (features.Graph)
                    nebula = sp.GetService<NebulaGraphExecutor>();
                return new IndexerCapabilities(nebula);
            });
        }

        // ---------- Graph ----------
        if (features.Graph)
        {
            services.AddScoped<KnowledgeGraphService>();
            services.AddSingleton<INebulaGraphMemoryStore, NebulaGraphMemoryStore>();
            services.AddSingleton<IMemoryStore>(sp =>
                sp.GetRequiredService<INebulaGraphMemoryStore>());
        }
        else
        {
            // Graph 关闭：提供 Null 兜底，保证 OrchestratorAgent 构造不失败
            services.AddSingleton<IMemoryStore, NullMemoryStore>();
        }

        // ---------- Entity ----------
        if (features.Entity)
        {
            services.AddScoped<IEntityExtractionService, EntityExtractionService>();
        }
        else
        {
            services.AddScoped<IEntityExtractionService, NullEntityExtractionService>();
        }

        return services;
    }
}