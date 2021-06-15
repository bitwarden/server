using System;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Settings;

namespace Bit.Core.Repositories.SqlServer
{
    public class ProviderOrganizationRepository : Repository<ProviderOrganization, Guid>, IProviderOrganizationRepository
    {
        public ProviderOrganizationRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public ProviderOrganizationRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }
    }
}
