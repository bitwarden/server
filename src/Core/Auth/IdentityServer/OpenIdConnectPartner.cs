namespace Bit.Core.Auth.IdentityServer;

/// <summary>
/// A pre-registered third-party "Login with Bitwarden" OIDC partner (e.g. an agentic bot integration),
/// resolved from operator configuration. Partner clients are authorization-code + PKCE clients restricted
/// to identity scopes with consent required; they never receive an <c>api</c> token or vault access.
/// </summary>
public class OpenIdConnectPartner
{
    /// <summary>Prefix that namespaces partner client ids (e.g. <c>partner.acme</c>).</summary>
    public const string ClientIdPrefix = "partner.";

    /// <summary>The fully-qualified client id, including the <see cref="ClientIdPrefix"/>.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Human-readable name shown to the user on the consent screen.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Base64 SHA-256 hash of the client secret.</summary>
    public string ClientSecretHash { get; init; } = string.Empty;

    /// <summary>Exact redirect URIs allowed for this client (the web vault broker callback[s]).</summary>
    public IReadOnlyList<string> RedirectUris { get; init; } = [];

    /// <summary>
    /// Allow-list of agent loopback redirect URIs the web vault broker may forward the agent-access PSK to.
    /// Loopback entries match any port (RFC 8252); non-loopback entries must match exactly.
    /// </summary>
    public IReadOnlyList<string> AgentRedirectUris { get; init; } = [];
}
