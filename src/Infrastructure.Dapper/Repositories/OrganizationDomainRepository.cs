using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationDomainRepository : Repository<OrganizationDomain, Guid>, IOrganizationDomainRepository
{
    public OrganizationDomainRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationDomainRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }
}
