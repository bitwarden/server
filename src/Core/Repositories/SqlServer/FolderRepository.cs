using System;
using Bit.Core.Models.Table;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using System.Linq;

namespace Bit.Core.Repositories.SqlServer
{
    public class FolderRepository : Repository<Folder, Guid>, IFolderRepository
    {
        public FolderRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public FolderRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task<Folder> GetByIdAsync(Guid id, Guid userId)
        {
            var folder = await GetByIdAsync(id);
            if(folder == null || folder.UserId != userId)
            {
                return null;
            }

            return folder;
        }

        public async Task<ICollection<Folder>> GetManyByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Folder>(
                    $"[{Schema}].[Folder_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
