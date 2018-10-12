using System;
using Bit.Core.Models.Table;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using System.Linq;
using System.Collections.Generic;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories.SqlServer
{
    public class OrganizationRepository : Repository<Organization, Guid>, IOrganizationRepository
    {
        public OrganizationRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public OrganizationRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task<ICollection<Organization>> GetManyByEnabledAsync()
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Organization>(
                    "[dbo].[Organization_ReadByEnabled]",
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<Organization>> GetManyByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Organization>(
                    "[dbo].[Organization_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<Organization>> SearchAsync(string name, string userEmail, bool? paid,
            int skip, int take)
        {
            using(var connection = new SqlConnection(ReadOnlyConnectionString))
            {
                var results = await connection.QueryAsync<Organization>(
                    "[dbo].[Organization_Search]",
                    new { Name = name, UserEmail = userEmail, Paid = paid, Skip = skip, Take = take },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 120);

                return results.ToList();
            }
        }

        public async Task UpdateStorageAsync(Guid id)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "[dbo].[Organization_UpdateStorage]",
                    new { Id = id },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 180);
            }
        }

        public async Task<ICollection<OrganizationAbility>> GetManyAbilitiesAsync()
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationAbility>(
                    "[dbo].[Organization_ReadAbilities]",
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
