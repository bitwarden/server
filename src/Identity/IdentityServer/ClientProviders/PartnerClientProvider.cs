#nullable enable

using Bit.Core;
using Bit.Core.Auth.IdentityServer;
using Bitwarden.Server.Sdk.Features;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer.ClientProviders;

/// <summary>
/// Resolves pre-registered "Login with Bitwarden" OIDC partner clients (client ids prefixed
/// <c>partner.</c>) from <see cref="IOpenIdConnectPartnerStore"/>. Gated by the OIDC-provider feature flag:
/// when disabled no partner client resolves, so the whole flow is inert.
/// </summary>
public class PartnerClientProvider : IClientProvider
{
    public const string PartnerPrefix = "partner";

    private readonly IOpenIdConnectPartnerStore _partnerStore;
    private readonly IFeatureService _featureService;

    public PartnerClientProvider(IOpenIdConnectPartnerStore partnerStore, IFeatureService featureService)
    {
        _partnerStore = partnerStore;
        _featureService = featureService;
    }

    public Task<Client?> GetAsync(string identifier)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.OpenIdConnectProvider))
        {
            return Task.FromResult<Client?>(null);
        }

        var partner = _partnerStore.GetByClientId(OpenIdConnectPartner.ClientIdPrefix + identifier);
        return Task.FromResult<Client?>(partner is null ? null : new OidcPartnerClient(partner));
    }
}
