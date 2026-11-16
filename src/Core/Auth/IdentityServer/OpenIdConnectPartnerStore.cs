using System.Collections.Frozen;
using Bit.Core.Settings;

namespace Bit.Core.Auth.IdentityServer;

/// <inheritdoc />
public class OpenIdConnectPartnerStore : IOpenIdConnectPartnerStore
{
    private readonly FrozenDictionary<string, OpenIdConnectPartner> _partners;

    public OpenIdConnectPartnerStore(GlobalSettings globalSettings)
    {
        var configured = globalSettings.IdentityServer.OpenIdConnectPartners ?? [];
        _partners = configured
            .Where(p => !string.IsNullOrWhiteSpace(p.ClientId))
            .Select(p => new OpenIdConnectPartner
            {
                ClientId = NormalizeClientId(p.ClientId),
                Name = p.Name ?? p.ClientId,
                ClientSecretHash = p.ClientSecretHash,
                RedirectUris = p.RedirectUris ?? [],
                AgentRedirectUris = p.AgentRedirectUris ?? [],
            })
            .ToFrozenDictionary(p => p.ClientId, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<OpenIdConnectPartner> GetAll() => _partners.Values;

    public OpenIdConnectPartner? GetByClientId(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        return _partners.TryGetValue(NormalizeClientId(clientId), out var partner) ? partner : null;
    }

    public bool IsRegistered(string clientId) => GetByClientId(clientId) is not null;

    private static string NormalizeClientId(string clientId) =>
        clientId.StartsWith(OpenIdConnectPartner.ClientIdPrefix, StringComparison.Ordinal)
            ? clientId
            : OpenIdConnectPartner.ClientIdPrefix + clientId;
}
