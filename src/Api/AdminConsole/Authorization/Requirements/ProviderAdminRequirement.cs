using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Enums.Provider;

namespace Bit.Api.AdminConsole.Authorization.Requirements;

/// <summary>
/// Authorizes ProviderAdmin users only.
/// </summary>
public class ProviderAdminRequirement : IProviderRequirement
{
    public Task<bool> AuthorizeAsync(CurrentContextProvider? providerClaims)
        => Task.FromResult(providerClaims?.Type == ProviderUserType.ProviderAdmin);
}
