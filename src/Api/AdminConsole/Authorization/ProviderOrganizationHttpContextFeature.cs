using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Api.AdminConsole.Authorization;

public static class ProviderOrganizationHttpContextFeature
{
    /// <summary>
    /// Returns the ProviderUserOrganizations for a user. These are the organizations the ProviderUser manages via their Provider, if any.
    /// This data is fetched from the database and cached as a HttpContext Feature for the lifetime of the request.
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="providerUserRepository"></param>
    /// <param name="userId"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Returns true if the user is a ProviderUser for a Provider which manages the specified organization, otherwise false.
    /// This data is fetched from the database and cached as a HttpContext Feature for the lifetime of the request.
    /// </summary>
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
