using System.Text.Json.Nodes;
using TreeGraph.IdentityAdmin.Auditing;
using TreeGraph.IdentityAdmin.Keycloak;
using TreeGraph.Shared.Identity.Admin;

namespace TreeGraph.IdentityAdmin.Modules;

/// <summary>realm 设置与 SMTP 管理实现。</summary>
public sealed class RealmSettingsAdminService : IRealmSettingsAdminService
{
    private const string Module = "realm";

    private readonly KeycloakAdminClient _client;
    private readonly AdminOperationRunner _runner;

    public RealmSettingsAdminService(KeycloakAdminClient client, AdminOperationRunner runner)
    {
        _client = client;
        _runner = runner;
    }

    public async Task<RealmSettings?> GetAsync(CancellationToken ct = default)
    {
        var response = await _client.GetAsync(string.Empty, Module, ct).ConfigureAwait(false);
        if (!response.IsSuccess)
            return null;

        var node = JsonNode.Parse(response.Body)?.AsObject();
        if (node is null)
            return null;

        var smtp = node["smtpServer"] as JsonObject;

        return new RealmSettings(
            ReadString(node, "realm") ?? _client.Realm,
            ReadString(node, "displayName"),
            ReadBool(node, "registrationAllowed"),
            ReadBool(node, "bruteForceProtected"),
            ReadString(node, "passwordPolicy"),
            ReadInt(node, "accessTokenLifespan"),
            ReadInt(node, "ssoSessionIdleTimeout"),
            ReadInt(node, "ssoSessionMaxLifespan"),
            ReadInt(node, "failureFactor"),
            ReadInt(node, "waitIncrementSeconds"),
            ReadInt(node, "maxFailureWaitSeconds"),
            ReadString(node, "loginTheme"),
            ReadString(node, "accountTheme"),
            ReadString(node, "adminTheme"),
            ReadString(node, "emailTheme"),
            ReadStringArray(node, "defaultGroups"),
            smtp is not null,
            smtp is null ? null : ReadString(smtp, "host"),
            smtp is null ? null : ReadString(smtp, "from"));
    }

    public async Task<AdminResult> UpdateAsync(
        AdminActor actor,
        UpdateRealmSettingsRequest request,
        CancellationToken ct = default)
    {
        var current = await _client.GetAsync(string.Empty, Module, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return KeycloakStatusMapper.ToResult(current.StatusCode);

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return AdminResult.Fail("realm 数据解析失败", 502);

        SetIfNotNull(node, "displayName", request.DisplayName);
        SetIfNotNull(node, "registrationAllowed", request.RegistrationAllowed);
        SetIfNotNull(node, "bruteForceProtected", request.BruteForceProtected);
        SetIfNotNull(node, "passwordPolicy", request.PasswordPolicy);
        SetIfNotNull(node, "accessTokenLifespan", request.AccessTokenLifespan);
        SetIfNotNull(node, "ssoSessionIdleTimeout", request.SsoSessionIdleTimeout);
        SetIfNotNull(node, "ssoSessionMaxLifespan", request.SsoSessionMaxLifespan);
        SetIfNotNull(node, "failureFactor", request.FailedLoginAttempts);
        SetIfNotNull(node, "waitIncrementSeconds", request.WaitIncrementSeconds);
        SetIfNotNull(node, "maxFailureWaitSeconds", request.LockoutDurationSeconds);
        SetIfNotNull(node, "loginTheme", request.LoginTheme);
        SetIfNotNull(node, "accountTheme", request.AccountTheme);
        SetIfNotNull(node, "adminTheme", request.AdminTheme);
        SetIfNotNull(node, "emailTheme", request.EmailTheme);

        if (request.DefaultGroups is not null)
        {
            var groups = new JsonArray();
            foreach (var group in request.DefaultGroups)
                groups.Add(group);

            node["defaultGroups"] = groups;
        }

        return await _runner.ExecuteAsync(
            actor,
            "realm.update",
            "realm",
            _client.Realm,
            request,
            token => _client.SendAsync(HttpMethod.Put, string.Empty, node, Module, token),
            ct).ConfigureAwait(false);
    }

    public async Task<AdminResult> UpdateSmtpAsync(
        AdminActor actor,
        SmtpSettingsRequest request,
        CancellationToken ct = default)
    {
        var current = await _client.GetAsync(string.Empty, Module, ct).ConfigureAwait(false);
        if (!current.IsSuccess)
            return KeycloakStatusMapper.ToResult(current.StatusCode);

        var node = JsonNode.Parse(current.Body)?.AsObject();
        if (node is null)
            return AdminResult.Fail("realm 数据解析失败", 502);

        var smtp = node["smtpServer"] as JsonObject ?? new JsonObject();
        smtp["host"] = request.Host;
        smtp["port"] = request.Port.ToString();
        smtp["starttls"] = request.StartTls ? "true" : "false";
        smtp["ssl"] = request.Ssl ? "true" : "false";

        if (request.From is not null)
            smtp["from"] = request.From;

        if (request.FromDisplayName is not null)
            smtp["fromDisplayName"] = request.FromDisplayName;

        if (request.User is not null)
            smtp["user"] = request.User;

        if (!string.IsNullOrEmpty(request.Password))
            smtp["password"] = request.Password;

        node["smtpServer"] = smtp;

        return await _runner.ExecuteAsync(
            actor,
            "realm.update-smtp",
            "realm",
            _client.Realm,
            request,
            token => _client.SendAsync(HttpMethod.Put, string.Empty, node, Module, token),
            ct).ConfigureAwait(false);
    }

    public async Task<AvailableThemes> GetAvailableThemesAsync(CancellationToken ct = default)
    {
        var response = await _client
            .SendAdminRootAsync(HttpMethod.Get, "serverinfo", Module, ct)
            .ConfigureAwait(false);

        if (!response.IsSuccess)
            return new AvailableThemes([], [], [], []);

        var node = JsonNode.Parse(response.Body)?.AsObject();
        var themes = node?["themes"] as JsonObject;
        if (themes is null)
            return new AvailableThemes([], [], [], []);

        return new AvailableThemes(
            ReadStringArray(themes, "login"),
            ReadStringArray(themes, "account"),
            ReadStringArray(themes, "admin"),
            ReadStringArray(themes, "email"));
    }

    private static string? ReadString(JsonObject node, string property)
        => (string?)node[property];

    private static bool ReadBool(JsonObject node, string property)
        => node[property] is JsonValue value && value.TryGetValue<bool>(out var parsed) && parsed;

    private static int ReadInt(JsonObject node, string property)
        => node[property] is JsonValue value && value.TryGetValue<int>(out var parsed) ? parsed : 0;

    private static IReadOnlyList<string> ReadStringArray(JsonObject node, string property)
        => node[property] is JsonArray array
            ? array.Select(item => (string?)item ?? string.Empty).Where(s => s.Length > 0).ToArray()
            : [];

    private static void SetIfNotNull(JsonNode node, string property, string? value)
    {
        if (value is not null)
            node[property] = value;
    }

    private static void SetIfNotNull(JsonNode node, string property, bool? value)
    {
        if (value is not null)
            node[property] = value.Value;
    }

    private static void SetIfNotNull(JsonNode node, string property, int? value)
    {
        if (value is not null)
            node[property] = value.Value;
    }
}
