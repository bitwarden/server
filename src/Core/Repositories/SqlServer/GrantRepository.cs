using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class GrantRepository : BaseRepository, IGrantRepository
    {
        public GrantRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public GrantRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task<Grant> GetByKeyAsync(string key)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Grant>(
                    "[dbo].[Grant_ReadByKey]",
                    new { Key = key },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }

        public async Task<ICollection<Grant>> GetManyAsync(string subjectId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Grant>(
                    "[dbo].[Grant_ReadBySubjectId]",
                    new { SubjectId = subjectId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task SaveAsync(Grant obj)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    "[dbo].[Grant_Save]",
                    obj,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task DeleteAsync(string key)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "[dbo].[Grant_DeleteByKey]",
                    new { Key = key },
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task DeleteAsync(string subjectId, string clientId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "[dbo].[Grant_DeleteBySubjectIdClientId]",
                    new { SubjectId = subjectId, ClientId = clientId },
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task DeleteAsync(string subjectId, string clientId, string type)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "[dbo].[Grant_DeleteBySubjectIdClientIdType]",
                    new { SubjectId = subjectId, ClientId = clientId, Type = type },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}
