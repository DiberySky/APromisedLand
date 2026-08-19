using APromisedLand.Api.NebulaGraph.Dtos;
using APromisedLand.Api.NebulaGraph.Services;
using Microsoft.AspNetCore.Mvc;

namespace APromisedLand.Api.NebulaGraph.ControllerBases;

/// <summary>
/// Mirrors <c>app.routers.users</c>: user CRUD, password change /
/// reset, and role grant / revoke across spaces.
/// </summary>
[ApiController]
[Route("users")]
public abstract class UsersControllerBase(NebulaFastApiService api) : ControllerBase
{
    /// <summary>List all users.</summary>
    [HttpGet("")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var res = await api.GetAsync("/users", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Create a user.</summary>
    [HttpPost("")]
    public async Task<IActionResult> Create([FromBody] UserCreateIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/users", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Describe a user.</summary>
    [HttpGet("{name}")]
    public async Task<IActionResult> Describe(string name, CancellationToken ct)
    {
        var res = await api.GetAsync($"/users/{Uri.EscapeDataString(name)}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Delete a user.</summary>
    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(
        string name, [FromQuery] bool ifExists = true, CancellationToken ct = default)
    {
        var res = await api.DeleteAsync(
            $"/users/{Uri.EscapeDataString(name)}",
            new[] { new KeyValuePair<string, string?>("if_exists", ifExists ? "true" : "false") },
            ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Change a user's password.</summary>
    [HttpPut("{name}/password")]
    public async Task<IActionResult> ChangePassword(
        string name, [FromBody] PasswordChangeIn body, CancellationToken ct)
    {
        body.Name = name;
        var res = await api.PutAsync(
            $"/users/{Uri.EscapeDataString(name)}/password", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Reset a user's password (admin).</summary>
    [HttpPost("{name}/password/reset")]
    public async Task<IActionResult> ResetPassword(
        string name, [FromBody] PasswordResetIn body, CancellationToken ct)
    {
        body.Name = name;
        var res = await api.PostAsync(
            $"/users/{Uri.EscapeDataString(name)}/password/reset", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    // ----------------------------------------------------------------- //
    // Roles
    // ----------------------------------------------------------------- //

    /// <summary>Show roles in a space.</summary>
    [HttpGet("roles/{space}")]
    public async Task<IActionResult> ShowRolesInSpace(string space, CancellationToken ct)
    {
        var res = await api.GetAsync($"/users/roles/{Uri.EscapeDataString(space)}", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Show a user's roles across spaces.</summary>
    [HttpGet("{name}/roles")]
    public async Task<IActionResult> ShowUserRoles(string name, CancellationToken ct)
    {
        var res = await api.GetAsync(
            $"/users/{Uri.EscapeDataString(name)}/roles", ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Grant a role to a user.</summary>
    [HttpPost("roles/grant")]
    public async Task<IActionResult> GrantRole([FromBody] RoleAssignIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/users/roles/grant", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }

    /// <summary>Revoke a role from a user.</summary>
    [HttpPost("roles/revoke")]
    public async Task<IActionResult> RevokeRole([FromBody] RoleRevokeIn body, CancellationToken ct)
    {
        var res = await api.PostAsync("/users/roles/revoke", body, ct: ct);
        return StatusCode(res.StatusCode, res.Body);
    }
}
