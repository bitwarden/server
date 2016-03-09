using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Domains;
using Bit.Core.Repositories.SqlServer.Models;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class FolderRepository : Repository<Folder, FolderTableModel>, IFolderRepository
    {
        public FolderRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<Folder> GetByIdAsync(string id, string userId)
        {
            var folder = await GetByIdAsync(id);
            if(folder == null || folder.UserId != userId)
            {
                return null;
            }

            return folder;
        }

        public async Task<ICollection<Folder>> GetManyByUserIdAsync(string userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<FolderTableModel>(
                    $"[{Schema}].[{Table}_ReadByUserId]",
                    new { UserId = new Guid(userId) },
                    commandType: CommandType.StoredProcedure);

                return results.Select(f => f.ToDomain()).ToList();
            }
        }
    }
}
