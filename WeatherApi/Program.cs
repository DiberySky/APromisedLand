using APromisedLand.Shared.Helper;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.AddServiceDefaults();

var port = AuthHelper.KeyCloakPort;

builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName:  "keycloak",
        realm:  "apromisedland",
        options =>
        {
            options.Authority = AuthHelper.Authority; //"https://localhost:8088/realms/apromisedland";
            options.Audience = "diberysky";
            // if (builder.Environment.IsDevelopment())
            {
                options.RequireHttpsMetadata = false;
            }
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

app.Run();