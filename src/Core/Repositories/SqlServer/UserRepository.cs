using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Domains;
using Dapper;
using StackExchange.Redis.Extensions.Core;

namespace Bit.Core.Repositories.SqlServer
{
    public class UserRepository : Repository<User, Guid>, IUserRepository
    {
        private readonly ICacheClient _cacheClient;

        public UserRepository(GlobalSettings globalSettings, ICacheClient cacheClient)
            : this(globalSettings.SqlServer.ConnectionString, cacheClient)
        { }

        public UserRepository(string connectionString, ICacheClient cacheClient)
            : base(connectionString)
        {
            _cacheClient = cacheClient;
        }

        public override async Task<User> GetByIdAsync(Guid id)
        {
            var cacheKey = string.Format(Constants.UserIdCacheKey, id);

            var user = await _cacheClient.GetAsync<User>(cacheKey);
            if(user != null)
            {
                return user;
            }

            user = await base.GetByIdAsync(id);
            await _cacheClient.AddAsync(cacheKey, user);
            return user;
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
            await PurgeCacheAsync(user);
        }

        public override async Task DeleteAsync(User user)
        {
            await base.DeleteAsync(user);
            await PurgeCacheAsync(user);
        }

        private async Task PurgeCacheAsync(User user)
        {
            await _cacheClient.RemoveAllAsync(new string[] { string.Format(Constants.UserIdCacheKey, user.Id) });
        }
    }
}
