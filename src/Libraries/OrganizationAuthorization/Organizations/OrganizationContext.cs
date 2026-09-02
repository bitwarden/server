using System.Security.Claims;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Context;
using Bit.Core.Services;

// Note: do not move this into Core! See remarks below.
namespace Bit.Api.AdminConsole.Authorization;

/// <summary>
/// Provides information about a user's membership or provider relationship with an organization.
/// Used for authorization decisions in the API layer, usually called by a controller or authorization handler or attribute.
/// </summary>
/// <remarks>
/// This is intended to deprecate organization-related methods in <see cref="ICurrentContext"/>.
/// It should remain in the API layer (not Core) because it is closely tied to user claims and authentication.
/// </remarks>
public interface IOrganizationContext
{
    /// <summary>
    /// Parses the provided <see cref="ClaimsPrincipal"/> for claims relating to the specified organization.
    /// A user will have organization claims if they are a confirmed member of the organization.
    /// </summary>
    /// <param name="user">The claims for the user.</param>
    /// <param name="organizationId">The organization to extract claims for.</param>
    /// <returns>
    /// A <see cref="CurrentContextOrganization"/> representing the user's claims for the organization,
    /// or null if the user has no claims.
    /// </returns>
    public CurrentContextOrganization? GetOrganizationClaims(ClaimsPrincipal user, Guid organizationId);
    /// <summary>
    /// Used to determine whether the user is a ProviderUser for the specified organization.
    /// </summary>
    /// <param name="user">The claims for the user.</param>
    /// <param name="organizationId">The organization to check the provider relationship for.</param>
    /// <returns>True if the user is a ProviderUser for the specified organization, otherwise false.</returns>
    /// <remarks>
    /// This requires a database call, but the results are cached for the lifetime of the service instance.
    /// Try to check purely claims-based sources of authorization first (such as organization membership with
    /// <see cref="GetOrganizationClaims"/>) to avoid unnecessary database calls.
    /// </remarks>
    public Task<bool> IsProviderUserForOrganization(ClaimsPrincipal user, Guid organizationId);
}

public class OrganizationContext(
    IUserService userService,
    IProviderUserRepository providerUserRepository) : IOrganizationContext
{
    public const string NoUserIdError = "This method should only be called on the private api with a logged in user.";

    /// <summary>
    /// Caches provider relationships by UserId.
    /// In practice this should only have 1 entry (for the current user), but this approach ensures that a mix-up
    /// between users cannot occur if <see cref="IsProviderUserForOrganization"/> is called with a different
    /// ClaimsPrincipal for any reason.
    /// </summary>
    private readonly Dictionary<Guid, IEnumerable<ProviderUserOrganizationDetails>> _providerUserOrganizationsCache = new();

    public CurrentContextOrganization? GetOrganizationClaims(ClaimsPrincipal user, Guid organizationId)
    {
        return user.GetCurrentContextOrganization(organizationId);
    }

    public async Task<bool> IsProviderUserForOrganization(ClaimsPrincipal user, Guid organizationId)
    {
        var userId = userService.GetProperUserId(user);
        if (!userId.HasValue)
        {
            throw new InvalidOperationException(NoUserIdError);
        }

        if (!_providerUserOrganizationsCache.TryGetValue(userId.Value, out var providerUserOrganizations))
        {
            providerUserOrganizations =
                await providerUserRepository.GetManyOrganizationDetailsByUserAsync(userId.Value,
                    ProviderUserStatusType.Confirmed);
            providerUserOrganizations = providerUserOrganizations.ToList();
            _providerUserOrganizationsCache[userId.Value] = providerUserOrganizations;
        }

        return providerUserOrganizations.Any(o => o.OrganizationId == organizationId);
    }
}
