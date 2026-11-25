using System.Net;
using System.Net.Http.Headers;

namespace TreeGraph.Blazor.Infrastructure.Identity;

public sealed class BearerAuthenticationHandler(WasmOidcSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await session.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized
            || request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options)
            return response;

        var refreshed = await session.GetAccessTokenAfterUnauthorizedAsync(token, cancellationToken);
        if (string.IsNullOrEmpty(refreshed) || refreshed == token) return response;

        response.Dispose();
        using var retry = CloneIdempotentRequest(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static HttpRequestMessage CloneIdempotentRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };
        foreach (var header in request.Headers.Where(h => !h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)))
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        foreach (var option in request.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        return clone;
    }
}
