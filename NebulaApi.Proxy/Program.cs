using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NebulaApi.Proxy.Models;
using NebulaApi.Proxy.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Bind the upstream FastAPI configuration.
builder.Services
    .AddOptions<NebulaFastApiOptions>()
    .Bind(builder.Configuration.GetSection("NebulaApi"));

// Typed HttpClient for proxying to the FastAPI + nebula-python
// service. The NebulaApiService constructor also enforces
// BaseAddress / timeout / bearer-token defaults, but setting them
// here keeps the configuration visible in one place.
var nebulaGraphFastApi = builder.Configuration["NebulaGraph-FastApi-Endpoint"];

builder.Services.AddHttpClient<NebulaFastApiService>(c =>
{
    var opt = builder.Configuration
        .GetSection("NebulaApi")
        .Get<NebulaFastApiOptions>() ?? new NebulaFastApiOptions();
    
    // 优先使用 nebulaGraphFastApi，如果为空则回退到 opt.BaseUrl
    var baseUrl = !string.IsNullOrEmpty(nebulaGraphFastApi) 
        ? nebulaGraphFastApi 
        : opt.BaseUrl;
    
    if (string.IsNullOrEmpty(baseUrl))
        throw new InvalidOperationException("BaseUrl for NebulaFastApi is not configured.");
    
    c.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
});

// builder.Services
//     .AddHttpClient<NebulaFastApiService>(c =>
//     {
//         var opt = builder.Configuration
//             .GetSection("NebulaApi")
//             .Get<NebulaFastApiOptions>() ?? new NebulaFastApiOptions();
//         c.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
//         c.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
//     });

// Snake_case JSON for both inbound model binding and outbound
// responses, mirroring the FastAPI Pydantic schemas.
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();

// Swagger / OpenAPI: also surface the XML doc comments written on the
// controllers and DTOs so the Swagger UI shows the same summaries as
// the FastAPI /docs page.
builder.Services.AddSwaggerGen(c =>
{
    // The FastAPI service accidentally registers POST /spaces/{space}/edges
    // twice (schema.create_edge and edges.insert_edges). FastAPI lets the
    // first-registered route shadow the second; mirror that behaviour in
    // the OpenAPI document by keeping only the first action when a
    // (method, path) collision is detected.
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

// CORS: mirror the permissive policy of the FastAPI service.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()));

var app = builder.Build();

// Expose /swagger and /swagger/v1/swagger.json in any environment so
// operators can inspect the proxy surface (the upstream FastAPI /docs
// remains the source of truth for the underlying nGQL surface).
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.MapDefaultEndpoints();

// Redirect "/" to the Swagger UI for discoverability.
app.MapGet("/", () => Results.Redirect("/swagger/index.html"));

app.Run();
