using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace APromisedLand.Shared.Services;

public partial class ProjectService
{
    public static string? UserName { get; set; }
    public static string? UserId { get; set; }

    public void GetClaimValue(IEnumerable<Claim> claims)
    {
        // Keycloak → .NET 标准映射
        //MapClaim(claims, "given_name", ClaimTypes.GivenName);
        //MapClaim(claims, "family_name", ClaimTypes.Surname);
        //MapClaim(claims, "preferred_username", ClaimTypes.Name);
        //MapClaim(claims, "email", ClaimTypes.Email);

        var given = claims.FirstOrDefault(x => x.Type == "given_name")?.Value;
        var family = claims.FirstOrDefault(x => x.Type == "family_name")?.Value;
        UserName = string.Concat(family, given);

        UserId = claims.FirstOrDefault(x => x.Type == "sid")?.Value;
    }
}
