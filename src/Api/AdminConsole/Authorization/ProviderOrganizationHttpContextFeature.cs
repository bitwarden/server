using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Api.AdminConsole.Authorization;

public static class ProviderOrganizationHttpContextFeature
{
    private static async Task<IEnumerable<ProviderUserOrganizationDetails>> GetProviderUserOrganizationsAsync(
        this HttpContext httpContext,
        IProviderUserRepository providerUserRepository,
        Guid userId)
    {
        var providerUserOrganizations = httpContext.Features.Get<IEnumerable<ProviderUserOrganizationDetails>>();
        if (providerUserOrganizations != null)
        {
            return providerUserOrganizations;
        }

        providerUserOrganizations = (await providerUserRepository.GetManyOrganizationDetailsByUserAsync(
            userId, ProviderUserStatusType.Confirmed)).ToList();
        httpContext.Features.Set(providerUserOrganizations);

        return providerUserOrganizations;
    }

    public static async Task<bool> IsProviderUserForOrgAsync(
        this HttpContext httpContext,
        IProviderUserRepository providerUserRepository,
        Guid userId,
        Guid organizationId)
    {
        var organizations = await httpContext.GetProviderUserOrganizationsAsync(providerUserRepository, userId);
        return organizations.Any(o => o.OrganizationId == organizationId);
    }
}
