namespace APromisedLand.Shared.Interfaces;

public interface IAuthenticationService
{
    Task<bool> LoginAsync(string username, string password);

    Task LogoutAsync();
}
