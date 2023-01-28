using Bit.Core.Settings;
using Bit.Core.Utilities;
using IdentityServer4.Configuration;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using IdentityServer4.Validation;

namespace Bit.Sso.Utilities;

public class DiscoveryResponseGenerator : IdentityServer4.ResponseHandling.DiscoveryResponseGenerator
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
        return CoreHelpers.AdjustIdentityServerConfig(dict, _globalSettings.BaseServiceUri.Sso,
            _globalSettings.BaseServiceUri.InternalSso);
    }
}
