using Bit.Core.Entities;

namespace Bit.Core.Auth.UserFeatures.PremiumAccess;

/// <summary>
/// Query for checking premium access status for users.
/// This is the centralized location for determining if a user can access premium features
/// (either through personal subscription or organization membership).
/// 
/// <para>
/// <strong>Note:</strong> This is different from checking User.Premium, which only indicates
/// personal subscription status. Use these methods to check actual premium feature access.
/// </para>
/// </summary>
public interface IPremiumAccessQuery
{
    /// <summary>
    /// Checks if a user has access to premium features (personal subscription or organization).
    /// This is the definitive way to check premium access for a single user.
    /// </summary>
    /// <param name="user">The user to check for premium access</param>
    /// <returns>True if user can access premium features; false otherwise</returns>
    Task<bool> CanAccessPremiumAsync(User user);

    /// <summary>
    /// Checks if a user has access to premium features through organization membership only.
    /// This is useful for determining the source of premium access (personal vs organization).
    /// </summary>
    /// <param name="userId">The user ID to check for organization premium access</param>
    /// <returns>True if user has premium access through any organization; false otherwise</returns>
    Task<bool> HasPremiumFromOrganizationAsync(Guid userId);

    /// <summary>
    /// Checks if multiple users have access to premium features (optimized bulk operation).
    /// Uses cached organization abilities and minimizes database queries.
    /// </summary>
    /// <param name="users">The users to check for premium access</param>
    /// <returns>Dictionary mapping user IDs to their premium access status (personal or through organization)</returns>
    Task<Dictionary<Guid, bool>> CanAccessPremiumAsync(IEnumerable<User> users);
}

