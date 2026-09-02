using Bit.Core.Billing.Providers.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Providers.Repositories;

public interface IClientOrganizationMigrationRecordRepository : IRepository<ClientOrganizationMigrationRecord, Guid>
{
    Task<ClientOrganizationMigrationRecord> GetByOrganizationId(Guid organizationId);
    Task<ICollection<ClientOrganizationMigrationRecord>> GetByProviderId(Guid providerId);
}
