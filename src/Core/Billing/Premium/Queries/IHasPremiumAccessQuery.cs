namespace Bit.Core.Billing.Premium.Queries;

/// <summary>
/// Centralized query for checking if users have premium access through personal subscriptions or organizations.
/// Note: Different from User.Premium which only checks personal subscriptions.
/// </summary>
public interface IHasPremiumAccessQuery
{
    /// <summary>
    /// Checks if a user has premium access (personal or organization).
    /// </summary>
    /// <param name="userId">The user ID to check</param>
    /// <returns>True if user can access premium features</returns>
    Task<bool> HasPremiumAccessAsync(Guid userId);

    /// <summary>
    /// Checks premium access for multiple users.
    /// </summary>
    /// <param name="userIds">The user IDs to check</param>
    /// <returns>Dictionary mapping user IDs to their premium access status</returns>
    Task<Dictionary<Guid, bool>> HasPremiumAccessAsync(IEnumerable<Guid> userIds);

    /// <summary>
    /// Checks if a user belongs to any organization that grants premium (enabled org with UsersGetPremium).
    /// Returns true regardless of personal subscription. Useful for UI decisions like showing subscription options.
    /// </summary>
    /// <param name="userId">The user ID to check</param>
    /// <returns>True if user is in any organization that grants premium</returns>
    Task<bool> HasPremiumFromOrganizationAsync(Guid userId);
}
