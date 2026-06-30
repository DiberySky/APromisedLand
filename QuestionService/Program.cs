using APromisedLand.Shared.Services;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using QuestionService.Data;
using QuestionService.Services;
using Wolverine;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TagService>();

builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName:  "keycloak",
        realm:  "apromisedland",
        options =>
        {
            options.TokenValidationParameters.ValidateIssuer = true;
            options.Authority = ProjectService.Authority; 
            options.Audience = "diberysky";
            if (builder.Environment.IsDevelopment())
            {
                options.RequireHttpsMetadata = false;
            }
        });

builder.AddNpgsqlDbContext<QuestionDbContext>("questionDb");

builder.Services.AddOpenTelemetry().WithTracing(traceProviderBuilder =>
{
    traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
            .AddService(builder.Environment.ApplicationName))
        .AddSource("Wolverine");
});

builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMqUsingNamedConnection("Messaging").AutoProvision();
    opts.PublishAllMessages().ToRabbitExchange("questions");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.MapDefaultEndpoints();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    var context = services.GetRequiredService<QuestionDbContext>();
    await context.Database.MigrateAsync();
}
catch (Exception e)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(e, "在迁移或初始化数据库时出现了错误。");
}

app.Run();