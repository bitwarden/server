using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
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
    public const string CacheName = "EventIntegrations";

    /// <summary>
    /// Builds a deterministic cache key for a <see cref="Group"/>.
    /// </summary>
    /// <param name="groupId">The unique identifier of the group.</param>
    /// <returns>
    /// A cache key for this Group.
    /// </returns>
    public static string BuildCacheKeyForGroup(Guid groupId) =>
        $"Group:{groupId:N}";

    /// <summary>
    /// Builds a deterministic cache key for an <see cref="Organization"/>.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <returns>
    /// A cache key for the Organization.
    /// </returns>
    public static string BuildCacheKeyForOrganization(Guid organizationId) =>
        $"Organization:{organizationId:N}";

    /// <summary>
    /// Builds a deterministic cache key for an organization user <see cref="OrganizationUserUserDetails"/>.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to which the user belongs.</param>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>
    /// A cache key for the user.
    /// </returns>
    public static string BuildCacheKeyForOrganizationUser(Guid organizationId, Guid userId) =>
        $"OrganizationUserUserDetails:{organizationId:N}:{userId:N}";

    /// <summary>
    /// Builds a deterministic cache key for an organization's integration configuration details
    /// <see cref="OrganizationIntegrationConfigurationDetails"/>.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to which the user belongs.</param>
    /// <param name="integrationType">The <see cref="IntegrationType"/> of the integration.</param>
    /// <param name="eventType">The <see cref="EventType"/> of the event configured. Can be null to apply to all events.</param>
    /// <returns>
    /// A cache key for the configuration details.
    /// </returns>
    public static string BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
        Guid organizationId,
        IntegrationType integrationType,
        EventType? eventType
    ) => $"OrganizationIntegrationConfigurationDetails:{organizationId:N}:{integrationType}:{eventType}";

    /// <summary>
    /// Builds a deterministic tag for tagging an organization's integration configuration details. This tag is then
    /// used to tag all of the <see cref="OrganizationIntegrationConfigurationDetails"/> that result from this
    /// integration, which allows us to remove all relevant entries when an integration is changed or removed.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization to which the user belongs.</param>
    /// <param name="integrationType">The <see cref="IntegrationType"/> of the integration.</param>
    /// <returns>
    /// A cache tag to use for the configuration details.
    /// </returns>
    public static string BuildCacheTagForOrganizationIntegration(
        Guid organizationId,
        IntegrationType integrationType
    ) => $"OrganizationIntegration:{organizationId:N}:{integrationType}";
}
