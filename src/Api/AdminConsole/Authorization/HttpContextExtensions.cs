using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Api.AdminConsole.Authorization;

public static class HttpContextExtensions
{
    public const string NoOrgIdError =
        "A route decorated with with '[Authorize<IOrganizationRequirement>]' must include a route value named 'orgId' or 'organizationId' either through the [Controller] attribute or through a '[Http*]' attribute.";

    public const string NoProviderIdError =
        "A route decorated with '[Authorize<IProviderRequirement>]' must include a route value named 'providerId' either through the [Controller] attribute or through a '[Http*]' attribute.";

    /// <param name="httpContext"></param>
    extension(HttpContext httpContext)
    {
        /// <summary>
        /// Returns the result of the callback, caching it in HttpContext.Features for the lifetime of the request.
        /// Subsequent calls will retrieve the cached value.
        /// Results are stored by type and therefore must be of a unique type.
        /// </summary>
        public async Task<T> WithFeaturesCacheAsync<T>(Func<Task<T>> callback)
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
        /// </summary>
        /// <remarks>
        /// This data is fetched from the database and cached as a HttpContext Feature for the lifetime of the request.
        /// </remarks>
        public async Task<bool> IsProviderUserForOrgAsync(IProviderUserRepository providerUserRepository,
            Guid userId,
            Guid organizationId)
        {
            var organizations = await httpContext.GetProviderUserOrganizationsAsync(providerUserRepository, userId);
            return organizations.Any(o => o.OrganizationId == organizationId);
        }

        /// <summary>
        /// Returns the ProviderUserOrganizations for a user. These are the organizations the ProviderUser manages via their Provider, if any.
        /// </summary>
        /// <remarks>
        /// This data is fetched from the database and cached as a HttpContext Feature for the lifetime of the request.
        /// </remarks>
        private async Task<IEnumerable<ProviderUserOrganizationDetails>> GetProviderUserOrganizationsAsync(IProviderUserRepository providerUserRepository,
            Guid userId)
            => await httpContext.WithFeaturesCacheAsync(() =>
                providerUserRepository.GetManyOrganizationDetailsByUserAsync(userId, ProviderUserStatusType.Confirmed));

        /// <summary>
        /// Parses the {orgId} or {organizationId} route parameter into a Guid, or throws if neither are present or are not valid guids.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public Guid GetOrganizationId()
            => httpContext.TryGetRouteParameterAsGuid("orgId")
               ?? httpContext.TryGetRouteParameterAsGuid("organizationId")
               ?? throw new InvalidOperationException(NoOrgIdError);

        /// <summary>
        /// Parses the {providerId} route parameter into a Guid, or throws if it is not present or is not a valid Guid.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public Guid GetProviderId()
            => httpContext.TryGetRouteParameterAsGuid("providerId")
               ?? throw new InvalidOperationException(NoProviderIdError);

        public Guid? TryGetRouteParameterAsGuid(string routeParameterName)
        {
            var routeValues = httpContext.GetRouteData().Values;

            if (routeValues.TryGetValue(routeParameterName, out var routeParam) &&
                routeParam != null &&
                Guid.TryParse(routeParam.ToString(), out var parsedGuid))
            {
                return parsedGuid;
            }

            return null;
        }
    }
}
