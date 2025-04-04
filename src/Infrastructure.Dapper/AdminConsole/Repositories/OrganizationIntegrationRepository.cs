using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationIntegrationRepository : Repository<OrganizationIntegration, Guid>, IOrganizationIntegrationRepository
{
    public OrganizationIntegrationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationIntegrationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }
}
