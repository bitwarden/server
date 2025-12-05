using Bit.Core.Auth.Models;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;


public interface ITwoFactorIsEnabledQuery
{
    /// <summary>
    /// Returns a list of user IDs and whether two factor is enabled for each user.
    /// </summary>
    /// <param name="userIds">The list of user IDs to check.</param>
    Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync(IEnumerable<Guid> userIds);
    /// <summary>
    /// Returns a list of users and whether two factor is enabled for each user.
    /// </summary>
    /// <param name="users">The list of users to check.</param>
    /// <typeparam name="T">The type of user in the list. Must implement <see cref="ITwoFactorProvidersUser"/>.</typeparam>
    Task<IEnumerable<(T user, bool twoFactorIsEnabled)>> TwoFactorIsEnabledAsync<T>(IEnumerable<T> users) where T : ITwoFactorProvidersUser;
    /// <summary>
    /// Returns whether two factor is enabled for the user. A user is able to have a TwoFactorProvider that is enabled but requires Premium.
    /// If the user does not have premium then the TwoFactorProvider is considered _not_ enabled.
    /// </summary>
    /// <param name="user">The user to check.</param>
    Task<bool> TwoFactorIsEnabledAsync(ITwoFactorProvidersUser user);

    /// <summary>
    /// Returns a list of user IDs and whether two factor is enabled for each user.
    /// This version uses PremiumAccessQuery with cached organization abilities for better performance.
    /// </summary>
    /// <param name="userIds">The list of user IDs to check.</param>
    Task<IEnumerable<(Guid userId, bool twoFactorIsEnabled)>> TwoFactorIsEnabledVNextAsync(IEnumerable<Guid> userIds);
    /// <summary>
    /// Returns a list of users and whether two factor is enabled for each user.
    /// This version uses PremiumAccessQuery with cached organization abilities for better performance.
    /// </summary>
    /// <param name="users">The list of users to check.</param>
    /// <typeparam name="T">The type of user in the list. Must implement <see cref="ITwoFactorProvidersUser"/>.</typeparam>
    Task<IEnumerable<(T user, bool twoFactorIsEnabled)>> TwoFactorIsEnabledVNextAsync<T>(IEnumerable<T> users) where T : ITwoFactorProvidersUser;
    /// <summary>
    /// Returns whether two factor is enabled for the user. A user is able to have a TwoFactorProvider that is enabled but requires Premium.
    /// If the user does not have premium then the TwoFactorProvider is considered _not_ enabled.
    /// This version uses PremiumAccessQuery with cached organization abilities for better performance.
    /// </summary>
    /// <param name="user">The user to check.</param>
    Task<bool> TwoFactorIsEnabledVNextAsync(ITwoFactorProvidersUser user);
}
