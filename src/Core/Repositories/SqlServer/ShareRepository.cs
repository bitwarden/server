using System;
using Bit.Core.Domains;
using System.Threading.Tasks;

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

        public async Task<Share> GetByIdAsync(Guid id, Guid userId)
        {
            var share = await GetByIdAsync(id);
            if(share == null || (share.UserId != userId && share.SharerUserId != userId))
            {
                return null;
            }

            return share;
        }
    }
}
