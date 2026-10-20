using Bit.Core.Providers.Context;
using Bit.Core.Providers.Enums;

namespace Bit.Api.AdminConsole.Authorization.Providers.Requirements;

/// <summary>
/// Authorizes ProviderAdmin users only.
/// </summary>
public class ProviderAdminRequirement : IProviderRequirement
{
    public bool Authorize(CurrentContextProvider? providerClaims)
        => providerClaims?.Type == ProviderUserType.ProviderAdmin;
}
