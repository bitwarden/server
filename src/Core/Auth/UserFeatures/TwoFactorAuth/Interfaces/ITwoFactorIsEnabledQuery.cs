using Bit.Core.Auth.Models;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;


public interface ITwoFactorIsEnabledQuery
{
    /// <summary>
    /// Returns a list of user IDs and whether two factor is enabled for each user.
    /// </summary>
    /// <param name="userIds">The list of user IDs to check.</param>
    Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync(IEnumerable<Guid> userIds);
    /// <summary>
    /// Returns a list of organization users and whether two factor is enabled for each user.
    /// Uses the pre-calculated HasPremiumAccess property from OrganizationUserUserDetails.
    /// </summary>
    Task<IEnumerable<(OrganizationUserUserDetails user, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync(IEnumerable<OrganizationUserUserDetails> users);
    /// <summary>
    /// Returns whether two factor is enabled for the user. A user is able to have a TwoFactorProvider that is enabled but requires Premium.
    /// If the user does not have premium then the TwoFactorProvider is considered _not_ enabled.
    /// </summary>
    /// <param name="user">The user to check.</param>
    Task<bool> TwoFactorIsEnabledAsync(ITwoFactorProvidersUser user);
}
