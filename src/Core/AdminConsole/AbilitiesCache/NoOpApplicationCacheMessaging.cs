using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public class NoOpApplicationCacheMessaging : IApplicationCacheServiceBusMessaging
{
    public Task NotifyOrganizationAbilityUpsertedAsync(Organization organization)
    {
        return Task.CompletedTask;
    }

    public Task NotifyOrganizationAbilityDeletedAsync(Guid organizationId)
    {
        return Task.CompletedTask;
    }

    public Task NotifyProviderAbilityDeletedAsync(Guid providerId)
    {
        return Task.CompletedTask;
    }
}
