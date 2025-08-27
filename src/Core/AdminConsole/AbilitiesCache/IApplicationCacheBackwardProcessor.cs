using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.AbilitiesCache;

/// <summary>
/// Temporary interface to provide legacy in-memory behavior to the service cache during architectural transition.
/// This interface will be removed once the migration to the new cache architecture is complete.
/// </summary>
public interface IApplicationCacheBackwardProcessor
{
    Task BaseUpsertOrganizationAbilityAsync(Organization organization);
    Task BaseDeleteOrganizationAbilityAsync(Guid organizationId);
}
