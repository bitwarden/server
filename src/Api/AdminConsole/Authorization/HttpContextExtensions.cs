#nullable enable

using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Api.AdminConsole.Authorization;

public static class HttpContextExtensions
{
    /// <summary>
    /// Returns the result of the callback, caching it in HttpContext.Features for the lifetime of the request.
    /// Subsequent calls will retrieve the cached value.
    /// Results are stored by type and therefore must be of a unique type.
    /// </summary>
    public static async Task<T> WithFeaturesCacheAsync<T>(this HttpContext httpContext, Func<Task<T>> callback)
    {
        var cachedResult = httpContext.Features.Get<T>();
        if (cachedResult != null)
        {
            return cachedResult;
        }

        var result = await callback();
        httpContext.Features.Set(result);

        return result;
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
        => await httpContext.WithFeaturesCacheAsync(async () =>
            (await providerUserRepository.GetManyOrganizationDetailsByUserAsync(
                userId, ProviderUserStatusType.Confirmed)).ToList());

}
