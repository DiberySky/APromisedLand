using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared.Intefaces;

public interface IAuthService
{
    Task<bool> LoginAsync(string username, string password);

    Task LogoutAsync();
}
