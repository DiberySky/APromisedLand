using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TreeGraph.Api.StringTreeSky.Data;
using TreeGraph.Api.StringTreeSky.Services;

namespace TreeGraph.Api.StringTreeSky.Extensions;

public static class StringTreeApiServiceExtensions
{
    public static IServiceCollection AddStringTreeApi(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDb)
    {
        services.AddDbContext<StringTreeDbContext>(configureDb);
        services.AddScoped<IStringTreeService, EfStringTreeService>();
        return services;
    }
}
