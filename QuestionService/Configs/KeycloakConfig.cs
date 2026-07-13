using APromisedLand.Shared.Services;
using SolutionService = APromisedLand.Shared.Services.Solution.SolutionService;

namespace QuestionService.Configs;

public static class KeycloakConfig
{
    public static void AddKeycloakService(this WebApplicationBuilder builder)
    {
        //string? Authority = Environment.GetEnvironmentVariable("Keycloak__Authority");

        var keycloakUrl = builder.Configuration["services:Keycloak:https:0"];
        if (string.IsNullOrEmpty(keycloakUrl))
        {
            Console.WriteLine("配置中未找到 Keycloak URI。");
            return;
        }

        var authority = $"{keycloakUrl}/realms/{SolutionService.Realm}";

        builder.Services.AddAuthentication()
            .AddKeycloakJwtBearer(
                serviceName: "keycloak",
                realm: "apromisedland",
                options =>
                {
                    options.TokenValidationParameters.ValidateIssuer = true;
                    options.Authority = authority;
                    options.Audience = "diberysky";
                    if (builder.Environment.IsDevelopment())
                    {
                        options.RequireHttpsMetadata = false;
                    }
                });
    }
}