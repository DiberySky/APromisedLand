namespace TreeGraph.IdentityAdmin.Modules.Internal;

/// <summary>Keycloak 用户表示（仅包含本系统使用的字段）。</summary>
internal sealed class KcUser
{
    public string? Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool? Enabled { get; set; }
    public bool? EmailVerified { get; set; }
    public long? CreatedTimestamp { get; set; }
    public List<string>? RequiredActions { get; set; }
    public Dictionary<string, List<string>>? Attributes { get; set; }
    public List<string>? Groups { get; set; }
    public List<string>? RealmRoles { get; set; }
}

/// <summary>Keycloak 角色表示。</summary>
internal sealed class KcRole
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? Composite { get; set; }
    public bool? ClientRole { get; set; }
    public string? ContainerId { get; set; }
}

/// <summary>Keycloak 组表示。</summary>
internal sealed class KcGroup
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? ParentId { get; set; }
    public Dictionary<string, List<string>>? Attributes { get; set; }
    public List<KcGroup>? SubGroups { get; set; }
}

/// <summary>Keycloak 客户端表示。</summary>
internal sealed class KcClient
{
    public string? Id { get; set; }
    public string? ClientId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? Enabled { get; set; }
    public bool? PublicClient { get; set; }
    public string? Protocol { get; set; }
    public string? ClientAuthenticatorType { get; set; }
    public List<string>? RedirectUris { get; set; }
    public List<string>? WebOrigins { get; set; }
    public bool? StandardFlowEnabled { get; set; }
    public bool? ImplicitFlowEnabled { get; set; }
    public bool? DirectAccessGrantsEnabled { get; set; }
    public bool? ServiceAccountsEnabled { get; set; }
    public string? RootUrl { get; set; }
    public string? BaseUrl { get; set; }
    public bool? FullScopeAllowed { get; set; }
    public bool? BearerOnly { get; set; }
    public List<string>? DefaultClientScopes { get; set; }
    public List<string>? OptionalClientScopes { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
}

/// <summary>Keycloak client scope 表示。</summary>
internal sealed class KcClientScope
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Protocol { get; set; }
}

/// <summary>Keycloak protocol mapper 表示。</summary>
internal sealed class KcProtocolMapper
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Protocol { get; set; }
    public string? ProtocolMapper { get; set; }
    public Dictionary<string, string>? Config { get; set; }
}

/// <summary>Keycloak 身份提供方表示。</summary>
internal sealed class KcIdentityProvider
{
    public string? Alias { get; set; }
    public string? ProviderId { get; set; }
    public string? DisplayName { get; set; }
    public bool? Enabled { get; set; }
    public bool? TrustEmail { get; set; }
    public bool? StoreToken { get; set; }
    public bool? LinkOnly { get; set; }
    public Dictionary<string, string>? Config { get; set; }
}

/// <summary>Keycloak 身份提供方 mapper 表示。</summary>
internal sealed class KcIdentityProviderMapper
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? IdentityProviderAlias { get; set; }
    public string? IdentityProviderMapper { get; set; }
    public Dictionary<string, string>? Config { get; set; }
}

/// <summary>Keycloak 组件表示（LDAP/AD 用户联邦）。</summary>
internal sealed class KcComponent
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? ProviderId { get; set; }
    public string? ProviderType { get; set; }
    public string? ParentId { get; set; }
    public Dictionary<string, List<string>>? Config { get; set; }
}

/// <summary>Keycloak 认证流表示。</summary>
internal sealed class KcAuthenticationFlow
{
    public string? Id { get; set; }
    public string? Alias { get; set; }
    public string? Description { get; set; }
    public string? ProviderId { get; set; }
    public bool? BuiltIn { get; set; }
    public bool? TopLevel { get; set; }
}

/// <summary>Keycloak 认证执行项表示。</summary>
internal sealed class KcAuthenticationExecutionInfo
{
    public string? Id { get; set; }
    public string? FlowId { get; set; }
    public string? DisplayName { get; set; }
    public string? ProviderId { get; set; }
    public string? Requirement { get; set; }
    public int? Priority { get; set; }
    public bool? AuthenticationFlow { get; set; }
    public string? FlowAlias { get; set; }
    public bool? Configurable { get; set; }
}

/// <summary>Keycloak 用户会话表示。</summary>
internal sealed class KcUserSession
{
    public string? Id { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? IpAddress { get; set; }
    public long? Start { get; set; }
    public long? LastAccess { get; set; }
    public Dictionary<string, string>? Clients { get; set; }
}

/// <summary>Keycloak 服务端信息（版本）。</summary>
internal sealed class KcServerInfo
{
    public string? SystemInfo { get; set; }
}

/// <summary>realm 根表示（仅取内部 ID 与名称）。</summary>
internal sealed class KcRealmRef
{
    public string? Id { get; set; }
    public string? Realm { get; set; }
}

/// <summary>Keycloak 用户同步结果。</summary>
internal sealed class KcSynchronizationResult
{
    public int? Added { get; set; }
    public int? Updated { get; set; }
    public int? Removed { get; set; }
    public int? Failed { get; set; }
    public int? Ignored { get; set; }
}

/// <summary>Keycloak 管理事件表示。</summary>
internal sealed class KcAdminEvent
{
    public long? Time { get; set; }
    public string? OperationType { get; set; }
    public string? ResourcePath { get; set; }
    public string? ResourceType { get; set; }
    public string? Error { get; set; }
    public string? IpAddress { get; set; }
    public string? ClientId { get; set; }
    public KcAuthDetails? AuthDetails { get; set; }
}

internal sealed class KcAuthDetails
{
    public string? UserId { get; set; }
    public string? ClientId { get; set; }
}

/// <summary>Keycloak 登录事件表示。</summary>
internal sealed class KcEvent
{
    public long? Time { get; set; }
    public string? Type { get; set; }
    public string? ClientId { get; set; }
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, string>? Details { get; set; }
}

/// <summary>Keycloak 密钥元数据（/admin/realms/{realm}/keys）。</summary>
internal sealed class KcKeyMetadata
{
    public string? ProviderId { get; set; }
    public int? ProviderPriority { get; set; }
    public string? Kid { get; set; }
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? Algorithm { get; set; }
    public string? PublicKey { get; set; }
    public string? Certificate { get; set; }
    public long? ValidTo { get; set; }
}

/// <summary>Keycloak 组织表示（Organizations）。</summary>
internal sealed class KcOrganization
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Alias { get; set; }
    public bool? Enabled { get; set; }
    public string? Description { get; set; }
    public string? RedirectUrl { get; set; }
    public List<string>? Domains { get; set; }
    public Dictionary<string, List<string>>? Attributes { get; set; }
}
