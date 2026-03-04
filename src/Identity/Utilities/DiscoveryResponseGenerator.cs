using Bit.Core.Settings;
using Bit.Core.Utilities;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;

namespace Bit.Identity.Utilities;

public class DiscoveryResponseGenerator : Duende.IdentityServer.ResponseHandling.DiscoveryResponseGenerator
{
    private readonly GlobalSettings _globalSettings;

    public DiscoveryResponseGenerator(
        IdentityServerOptions options,
        IResourceStore resourceStore,
        IKeyMaterialService keys,
        ExtensionGrantValidator extensionGrants,
        ISecretsListParser secretParsers,
        IResourceOwnerPasswordValidator resourceOwnerValidator,
        ILogger<DiscoveryResponseGenerator> logger,
        GlobalSettings globalSettings)
        : base(options, resourceStore, keys, extensionGrants, secretParsers, resourceOwnerValidator, logger)
    {
        _globalSettings = globalSettings;
    }

    public override async Task<Dictionary<string, object>> CreateDiscoveryDocumentAsync(
        string baseUrl, string issuerUri)
    {
        var dict = await base.CreateDiscoveryDocumentAsync(baseUrl, issuerUri);

        // Remove metadata for endpoints/features we don't support
        dict.Remove("revocation_endpoint_auth_methods_supported");
        dict.Remove("introspection_endpoint_auth_methods_supported");
        dict.Remove("backchannel_authentication_request_signing_alg_values_supported");

        return CoreHelpers.AdjustIdentityServerConfig(dict, _globalSettings.BaseServiceUri.Identity,
            _globalSettings.BaseServiceUri.InternalIdentity);
    }
}
