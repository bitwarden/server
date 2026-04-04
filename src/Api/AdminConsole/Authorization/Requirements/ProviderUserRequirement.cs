using Bit.Core.AdminConsole.Context;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

/// <summary>
/// Authorizes any provider member (ProviderAdmin or ServiceUser).
/// </summary>
public class ProviderUserRequirement : IProviderRequirement
{
    public Task<bool> AuthorizeAsync(CurrentContextProvider? providerClaims)
        => Task.FromResult(providerClaims != null);
}
