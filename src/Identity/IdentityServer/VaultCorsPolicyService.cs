using Bit.Core.Settings;
using Bit.Core.Utilities;
using Duende.IdentityServer.Services;

namespace Bit.Identity.IdentityServer;

public class CustomCorsPolicyService : ICorsPolicyService
{
    private readonly GlobalSettings _globalSettings;

    public CustomCorsPolicyService(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public Task<bool> IsOriginAllowedAsync(string origin)
    {
        return Task.FromResult(CoreHelpers.IsCorsOriginAllowed(origin, _globalSettings));
    }
}
