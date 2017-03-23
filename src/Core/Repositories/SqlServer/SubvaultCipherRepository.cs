using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using System.Linq;

namespace Bit.Core.Repositories.SqlServer
{
    public class SubvaultCipherRepository : BaseRepository, ISubvaultCipherRepository
    {
        public SubvaultCipherRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public SubvaultCipherRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<ICollection<SubvaultCipher>> GetManyByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SubvaultCipher>(
                    $"[dbo].[SubvaultCipher_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
