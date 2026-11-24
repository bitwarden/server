namespace Bit.Core.Auth.IdentityServer;

/// <summary>
/// Read-only registry of the pre-registered "Login with Bitwarden" OIDC partner clients, sourced from
/// operator configuration (<c>GlobalSettings.IdentityServer.OpenIdConnectPartners</c>). Shared by Identity
/// (to build Duende clients) and the API (to validate agent-connection writes against a known partner).
/// </summary>
public interface IOpenIdConnectPartnerStore
{
    /// <summary>All configured partners.</summary>
    IReadOnlyCollection<OpenIdConnectPartner> GetAll();

    /// <summary>The partner with the given (prefixed) client id, or <see langword="null"/> if none.</summary>
    OpenIdConnectPartner? GetByClientId(string clientId);

    /// <summary>True if <paramref name="clientId"/> maps to a configured partner.</summary>
    bool IsRegistered(string clientId);
}
