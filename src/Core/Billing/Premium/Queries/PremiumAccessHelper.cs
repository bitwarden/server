using Bit.Core.Entities;

namespace Bit.Core.Billing.Premium.Queries;

public static class PremiumAccessHelper
{
    /// <summary>
    /// Checks whether a user can use premium features through a personal subscription or an organization.
    /// Depends only on <see cref="IHasPremiumAccessQuery"/> so callers can use it without taking a dependency on
    /// <c>IUserService</c>, which would create a cycle for services that the user service itself depends on.
    /// </summary>
    public static async Task<bool> CanAccessPremiumAsync(User user, IHasPremiumAccessQuery hasPremiumAccessQuery)
    {
        var userId = user.GetUserId();
        if (!userId.HasValue)
        {
            return false;
        }

        return user.Premium || await hasPremiumAccessQuery.HasPremiumFromOrganizationAsync(userId.Value);
    }
}
