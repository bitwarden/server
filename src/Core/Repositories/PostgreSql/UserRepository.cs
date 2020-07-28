using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Dapper;
using Npgsql;

namespace Bit.Core.Repositories.PostgreSql
{
    public class UserRepository : Repository<User, Guid>, IUserRepository
    {
        public UserRepository(GlobalSettings globalSettings)
            : this(globalSettings.PostgreSql.ConnectionString, globalSettings.PostgreSql.ReadOnlyConnectionString)
        { }

        public UserRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public override async Task<User> GetByIdAsync(Guid id)
        {
            return await base.GetByIdAsync(id);
        }

        public async Task<User> GetByEmailAsync(string email)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<User>(
                    "user_read_by_email",
                    ToParam(new { Email = email }),
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public async Task<UserKdfInformation> GetKdfInformationByEmailAsync(string email)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<UserKdfInformation>(
                    "user_read_kdf_by_email",
                    ToParam(new { Email = email }),
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public async Task<ICollection<User>> SearchAsync(string email, int skip, int take)
        {
            using (var connection = new NpgsqlConnection(ReadOnlyConnectionString))
            {
                var results = await connection.QueryAsync<User>(
                    "user_search",
                    ToParam(new { Email = email, Skip = skip, Take = take }),
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 120);

                return results.ToList();
            }
        }

        public async Task<ICollection<User>> GetManyByPremiumAsync(bool premium)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<User>(
                    "user_read_by_premium",
                    ToParam(new { Premium = premium }),
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<User>> GetManyByPremiumRenewalAsync()
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<User>(
                    "user_read_by_premium_renewal",
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<string> GetPublicKeyAsync(Guid id)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<string>(
                    "user_read_public_key_by_id",
                    ToParam(new { Id = id }),
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public async Task<DateTime> GetAccountRevisionDateAsync(Guid id)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<DateTime>(
                    "user_read_account_revision_date_by_id",
                    ToParam(new { Id = id }),
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
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    $"user_delete_by_id",
                    ToParam(new { Id = user.Id }),
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 180);
            }
        }

        public async Task UpdateStorageAsync(Guid id)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "user_update_storage",
                    ToParam(new { Id = id }),
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 180);
            }
        }

        public async Task UpdateRenewalReminderDateAsync(Guid id, DateTime renewalReminderDate)
        {
            using (var connection = new NpgsqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "user_update_renewal_reminder_date",
                    ToParam(new { Id = id, RenewalReminderDate = renewalReminderDate }),
                    commandType: CommandType.StoredProcedure);
            }
        }

        public Task<User> GetBySsoUserAsync(string externalId, Guid? organizationId)
        {
            throw new NotImplementedException();
        }
    }
}
