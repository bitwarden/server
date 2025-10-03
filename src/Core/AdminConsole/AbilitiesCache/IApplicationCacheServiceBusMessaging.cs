using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public interface IApplicationCacheServiceBusMessaging
{
    Task NotifyOrganizationAbilityUpsertedAsync(Organization organization);
    Task NotifyOrganizationAbilityDeletedAsync(Guid organizationId);
    Task NotifyProviderAbilityDeletedAsync(Guid providerId);
}
