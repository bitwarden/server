using AutoMapper;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using EFClientOrganizationMigrationRecord = Bit.Infrastructure.EntityFramework.Billing.Models.ClientOrganizationMigrationRecord;

namespace Bit.Infrastructure.EntityFramework.Billing.Repositories;

public class ClientOrganizationMigrationRecordRepository(
    IMapper mapper,
    IServiceScopeFactory serviceScopeFactory)
    : Repository<ClientOrganizationMigrationRecord, EFClientOrganizationMigrationRecord, Guid>(
        serviceScopeFactory,
        mapper,
        context => context.ClientOrganizationMigrationRecords), IClientOrganizationMigrationRecordRepository
{
    public async Task<ClientOrganizationMigrationRecord> GetByOrganizationId(Guid organizationId)
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();

        var databaseContext = GetDatabaseContext(serviceScope);

        var query =
            from clientOrganizationMigrationRecord in databaseContext.ClientOrganizationMigrationRecords
            where clientOrganizationMigrationRecord.OrganizationId == organizationId
            select clientOrganizationMigrationRecord;

        return await query.FirstOrDefaultAsync();
    }

    public async Task<ICollection<ClientOrganizationMigrationRecord>> GetByProviderId(Guid providerId)
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();

        var databaseContext = GetDatabaseContext(serviceScope);

        var query =
            from clientOrganizationMigrationRecord in databaseContext.ClientOrganizationMigrationRecords
            where clientOrganizationMigrationRecord.ProviderId == providerId
            select clientOrganizationMigrationRecord;

        return await query.ToArrayAsync();
    }
}
