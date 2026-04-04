using Bit.Core.AdminConsole.Context;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

/// <summary>
/// Authorizes any provider member (ProviderAdmin or ServiceUser).
/// </summary>
public class ProviderUserRequirement : IProviderRequirement
{
    public bool Authorize(CurrentContextProvider? providerClaims)
        => providerClaims != null;
}
