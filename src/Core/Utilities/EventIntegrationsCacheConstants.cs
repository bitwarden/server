using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Utilities;

/// <summary>
/// Provides cache key generation helpers and cache name constants for event integration–related entities.
/// </summary>
public static class EventIntegrationsCacheConstants
{
    /// <summary>
    /// The base cache name used for storing event integration data.
    /// </summary>
    public static readonly string CacheName = "EventIntegrations";

    /// <summary>
    /// Builds a deterministic cache key for a <see cref="Group"/>.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <returns>
    /// A cache key for this Group.
    /// </returns>
    public static string BuildCacheKeyForGroup(Guid groupId)
    {
        return $"Group:{groupId:N}";
    }

    /// <summary>
    /// Builds a deterministic cache key for an <see cref="Organization"/>.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <returns>
    /// A cache key for the Organization.
    /// </returns>
    public static string BuildCacheKeyForOrganization(Guid organizationId)
    {
        return $"Organization:{organizationId:N}";
    }

    /// <summary>
    /// Builds a deterministic cache key for an organization user <see cref="OrganizationUserUserDetails"/>.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to which the user belongs.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>
    /// A cache key for the user.
    /// </returns>
    public static string BuildCacheKeyForOrganizationUser(Guid organizationId, Guid userId)
    {
        return $"OrganizationUserUserDetails:{organizationId:N}:{userId:N}";
    }
}
