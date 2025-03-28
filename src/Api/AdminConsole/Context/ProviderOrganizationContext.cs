using Bit.Core.Context;

namespace Bit.Api.AdminConsole.Context;

public class ProviderOrganizationContext(ICurrentContext currentContext) : IProviderOrganizationContext
{
    /// <inheritdoc/>
    public async Task<bool> ProviderUserForOrgAsync(Guid orgId)
    {
        // If the user doesn't have any ProviderUser claims (in relation to the provider), they can't have a provider
        // relationship to any organization.
        if (currentContext.Providers.Count == 0)
        {
            return false;
        }

        // This is just a wrapper around CurrentContext for now, but once permission checks are moved out of that class
        // we should be able to move the underlying logic here without causing circular dependencies.
        return await currentContext.ProviderUserForOrgAsync(orgId);
    }
}
