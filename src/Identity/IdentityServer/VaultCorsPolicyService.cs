using Bit.Core.Settings;
using Bit.Core.Utilities;
using IdentityServer4.Services;

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
