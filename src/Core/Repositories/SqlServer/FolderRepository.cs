using System;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories.SqlServer
{
    public class FolderRepository : Repository<Folder, Guid>, IFolderRepository
    {
        public FolderRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public FolderRepository(string connectionString)
            : base(connectionString)
        { }
    }
}
