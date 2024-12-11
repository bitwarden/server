using Bit.Core.Billing.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Repositories;

public interface IClientOrganizationMigrationRecordRepository
    : IRepository<ClientOrganizationMigrationRecord, Guid>
{
    Task<ClientOrganizationMigrationRecord> GetByOrganizationId(Guid organizationId);
    Task<ICollection<ClientOrganizationMigrationRecord>> GetByProviderId(Guid providerId);
}
