using System;
using Bit.Core.Domains;

namespace Bit.Core.Repositories.SqlServer
{
    public class ShareRepository : Repository<Share, Guid>, IShareRepository
    {
        public ShareRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public ShareRepository(string connectionString)
            : base(connectionString)
        { }
    }
}
