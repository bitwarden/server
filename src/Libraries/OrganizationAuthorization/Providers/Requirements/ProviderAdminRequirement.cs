using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Enums.Provider;

namespace Bit.Api.AdminConsole.Authorization.Providers.Requirements;

/// <summary>
/// Authorizes ProviderAdmin users only.
/// </summary>
public class ProviderAdminRequirement : IProviderRequirement
{
    public bool Authorize(CurrentContextProvider? providerClaims)
        => providerClaims?.Type == ProviderUserType.ProviderAdmin;
}
