using Typesense.Setup;

namespace QuestionService.Configs;

public static class TypesenseConfig
{
    public static void AddTypesensService(this WebApplicationBuilder builder)
    {
        // ---------- Typesense 配置 ----------
        var typesenseUri = builder.Configuration["services:typesense:typesense:0"];
        if (string.IsNullOrEmpty(typesenseUri))
            throw new InvalidOperationException("配置中未找到 Typesense URI。");

        var typesenseApiKey = builder.Configuration["typesense-api-key"];
        if (string.IsNullOrEmpty(typesenseApiKey))
            throw new InvalidOperationException("配置中未找到 Typesense API 密钥");

        var uri = new Uri(typesenseUri);
        builder.Services.AddTypesenseClient(config =>
        {
            config.ApiKey = typesenseApiKey;
            config.Nodes = new List<Node>
            {
                new(uri.Host, uri.Port.ToString(), uri.Scheme)
            };
        });
    }
}