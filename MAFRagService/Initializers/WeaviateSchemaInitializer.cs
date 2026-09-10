using System.Text;
using System.Text.Json;

﻿namespace MAFRagService.Initializers;

public class WeaviateSchemaInitializer
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<WeaviateSchemaInitializer> _logger;

    public WeaviateSchemaInitializer(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<WeaviateSchemaInitializer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("WeaviateClient");
        var baseUrl = _config["Weaviate:Url"] ?? "http://localhost:8081";
        var schemaUrl = $"{baseUrl}/v1/schema";

        var checkResp = await client.GetAsync($"{schemaUrl}/Document", ct);
        if (checkResp.IsSuccessStatusCode)
        {
            _logger.LogInformation("Weaviate class 'Document' already exists");
            return;
        }

        var schema = new
        {
            @class = "Document",
            vectorizer = "none",
            properties = new[]
            {
                new { name = "docId", dataType = new[] { "string" } },
                new { name = "tenant", dataType = new[] { "string" } },
                new { name = "version", dataType = new[] { "string" } },
                new { name = "chunkIndex", dataType = new[] { "int" } },
                new { name = "content", dataType = new[] { "text" } },
                new { name = "filePath", dataType = new[] { "string" } }
            }
        };

        var json = JsonSerializer.Serialize(schema);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(schemaUrl, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Weaviate init failed: {error}");
        }
        _logger.LogInformation("Weaviate schema initialized");
    }
}
