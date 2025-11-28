using Bit.Core.Settings;
using Bit.Core.Utilities;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using DIR = Duende.IdentityServer.ResponseHandling;

namespace Bit.Sso.Utilities;

public class DiscoveryResponseGenerator : DIR.DiscoveryResponseGenerator
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
