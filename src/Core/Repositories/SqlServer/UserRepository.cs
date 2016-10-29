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
        public UserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public UserRepository(string connectionString)
            : base(connectionString)
        { }

        public override async Task<User> GetByIdAsync(Guid id)
        {
            return await base.GetByIdAsync(id);
        }

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

        public override async Task ReplaceAsync(User user)
        {
            await base.ReplaceAsync(user);
        }

        public override async Task DeleteAsync(User user)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    $"[{Schema}].[{Table}_DeleteById]",
                    new { Id = user.Id },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 60);
            }
        }
    }
}
