using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories.SqlServer
{
    public class SubvaultUserRepository : Repository<SubvaultUser, Guid>, ISubvaultUserRepository
    {
        public SubvaultUserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public SubvaultUserRepository(string connectionString)
            : base(connectionString)
        { }
    }
}
