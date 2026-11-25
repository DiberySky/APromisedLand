using TreeGraph.Shared.Identity;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Api.Endpoints;

public static partial class AdminApiEndpointExtensions
{
    public static IEndpointRouteBuilder MapIdentityAdminApi(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin")
            .RequireAuthorization(IdentityConstants.AdminOnlyPolicy);

        admin.MapGet("/users", async (
                string? search,
                int? page,
                int? pageSize,
                bool? enabled,
                IUsersAdminService service,
                CancellationToken ct) =>
            Results.Ok(await service.SearchAsync(
                new AdminPageRequest(search, page ?? 1, pageSize ?? 25, enabled), ct)));

        admin.MapGet("/users/{id}", async (string id, IUsersAdminService service, CancellationToken ct) =>
        {
            var user = await service.GetAsync(id, ct);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        admin.MapPost("/users/{id}/disable", async (
                string id,
                IUsersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.DisableAsync(actorAccessor.GetRequiredActor(), id, ct)))
            .RequireStepUp();

        admin.MapPost("/users/{id}/enable", async (
                string id,
                IUsersAdminService service,
                IAdminActorAccessor actorAccessor,
                CancellationToken ct) =>
            ToHttpResult(await service.EnableAsync(actorAccessor.GetRequiredActor(), id, ct)));

        admin.MapDirectoryAdminApi();
        admin.MapClientAdminApi();
        admin.MapConfigurationAdminApi();

        return endpoints;
    }

    private static RouteHandlerBuilder RequireStepUp(this RouteHandlerBuilder builder)
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var stepUp = context.HttpContext.RequestServices.GetRequiredService<IStepUpService>();
            var state = stepUp.GetState(context.HttpContext.User);
            if (state.IsFresh)
                return await next(context);

            return Results.Problem(
                statusCode: StatusCodes.Status428PreconditionRequired,
                title: "需要重新认证后再执行此操作",
                extensions: new Dictionary<string, object?>
                {
                    ["error"] = "step_up_required",
                    ["maxAgeSeconds"] = state.MaxAgeSeconds
                });
        });

    private static IResult ToHttpResult(AdminResult result)
        => Results.Json(result, statusCode: HttpStatusCode(result));

    private static IResult ToHttpResult<T>(AdminResult<T> result)
        => Results.Json(result, statusCode: HttpStatusCode(result));

    private static int HttpStatusCode(AdminResult result)
        => result.Success ? StatusCodes.Status200OK : ValidErrorStatus(result.StatusCode);

    private static int HttpStatusCode<T>(AdminResult<T> result)
        => result.Success ? StatusCodes.Status200OK : ValidErrorStatus(result.StatusCode);

    private static int ValidErrorStatus(int? statusCode)
        => statusCode is >= 400 and <= 599 ? statusCode.Value : StatusCodes.Status502BadGateway;
}
