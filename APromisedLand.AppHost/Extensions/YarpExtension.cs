namespace APromisedLand.AppHost.Extensions;

public static class YarpExtension
{
    public static IDistributedApplicationBuilder AddYarp(
        this IDistributedApplicationBuilder builder,
        AppHostResourceContext resourceContext)
    {
        // if (resourceContext.WeatherApi is null || resourceContext.QuestionService is null ||
        //     resourceContext.TypesenseService is null || resourceContext.ElasticService is null ||
        //     resourceContext.FileTransService is null) return builder;

        resourceContext.YarpGateway = builder.AddYarp("Yarp")
            .WithConfiguration(yarp =>
            {
                if (resourceContext.WeatherApi is not null) 
                {
                    yarp.AddRoute("/WeatherForecast/{**catch-all}", resourceContext.WeatherApi);
                }
                if (resourceContext.QuestionService is not null) 
                {
                    yarp.AddRoute("/Questions/{**catch-all}", resourceContext.QuestionService);
                    yarp.AddRoute("/tags/{**catch-all}", resourceContext.QuestionService);
                }
                if (resourceContext.TypesenseService is not null) 
                {
                    yarp.AddRoute("/search-mini/{**catch-all}", resourceContext.TypesenseService);
                    yarp.AddRoute("/typesense/{**catch-all}", resourceContext.TypesenseService);
                }
                if (resourceContext.ElasticService is not null) 
                {
                    yarp.AddRoute("/elastic/{**catch-all}", resourceContext.ElasticService); 
                }
                if (resourceContext.FileTransService is not null) 
                {
                    yarp.AddRoute("/filetrans/{**catch-all}", resourceContext.FileTransService);
                }
                if (resourceContext.DiberyTreeService is not null) 
                {
                    yarp.AddRoute("/DiberyTree/{**catch-all}", resourceContext.DiberyTreeService);
                    yarp.AddRoute("/CategoryTree/{**catch-all}", resourceContext.DiberyTreeService);
                }
            })
            .WithHttpEndpoint(port: 8919, targetPort: 8919, name: "http")
            .WithOtlpExporter();

        return builder;
    }
}