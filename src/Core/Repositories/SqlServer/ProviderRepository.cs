using System;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Settings;

namespace Bit.Core.Repositories.SqlServer
{
    public class ProviderRepository : Repository<Provider, Guid>, IProviderRepository
    {
        public ProviderRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public ProviderRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }
    }
}
