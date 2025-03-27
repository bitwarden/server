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

        return await currentContext.ProviderUserForOrgAsync(orgId);
    }
}
