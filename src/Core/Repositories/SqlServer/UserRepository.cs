using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Domains;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class UserRepository : Repository<User, Guid>, IUserRepository
    {
        public UserRepository(string connectionString)
            : base(connectionString)
        { }

        public async Task<User> GetByEmailAsync(string email)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<User>(
                    $"[{Schema}].[{Table}_ReadByEmail]",
                    new { Email = email },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }
    }
}
