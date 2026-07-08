namespace APromisedLand.Shared.Interfaces;

public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password);

    Task LogoutAsync();
}
