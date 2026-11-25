using Microsoft.AspNetCore.Components.Authorization;

namespace TreeGraph.Blazor.Infrastructure.Identity;

public sealed class WasmAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly WasmOidcSession _session;

    public WasmAuthenticationStateProvider(WasmOidcSession session)
    {
        _session = session;
        _session.AuthenticationChanged += OnAuthenticationChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(_session.CurrentAuthenticationState);

    private void OnAuthenticationChanged()
        => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void Dispose() => _session.AuthenticationChanged -= OnAuthenticationChanged;
}
