using MAFRagService.Memory;
using MAFRagService.Services;
using MAFRagService.Startup.Configuration;
using MAFRagService.Stubs.MAF;

namespace MAFRagService.Startup.Extensions;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddRagBusinessServices(
        this IServiceCollection services,
        FeatureFlags features)
    {
        if (features.Rag)
        {
            services.AddScoped<DocumentMetadataService>();
            services.AddScoped<DocumentStorageService>();
            services.AddScoped<RagService>();
            services.AddScoped<VersionManager>();
        }

        if (features.Indexing)
        {
            services.AddScoped<EventStoreService>();
            services.AddScoped<IncrementalIndexer>();
        }

        if (features.Graph)
        {
            services.AddScoped<KnowledgeGraphService>();
            services.AddSingleton<INebulaGraphMemoryStore, NebulaGraphMemoryStore>();
            services.AddSingleton<IMemoryStore>(sp =>
                sp.GetRequiredService<INebulaGraphMemoryStore>());
        }

        if (features.Entity)
        {
            services.AddScoped<IEntityExtractionService, EntityExtractionService>();
        }

        return services;
    }
}