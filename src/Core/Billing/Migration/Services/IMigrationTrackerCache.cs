using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.Billing.Migration.Models;

namespace Bit.Core.Billing.Migration.Services;

public interface IMigrationTrackerCache
{
    Task StartTracker(Provider provider);
    Task SetOrganizationIds(Guid providerId, IEnumerable<Guid> organizationIds);
    Task<ProviderMigrationTracker> GetTracker(Guid providerId);
    Task UpdateTrackingStatus(Guid providerId, ProviderMigrationProgress status);

    Task StartTracker(Guid providerId, Organization organization);
    Task<ClientMigrationTracker> GetTracker(Guid providerId, Guid organizationId);
    Task UpdateTrackingStatus(Guid providerId, Guid organizationId, ClientMigrationProgress status);
}
