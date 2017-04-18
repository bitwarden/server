using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using System.Linq;
using Bit.Core.Utilities;

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
                    "[dbo].[SubvaultCipher_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<SubvaultCipher>> GetManyByOrganizationIdAsync(Guid organizationId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SubvaultCipher>(
                    "[dbo].[SubvaultCipher_ReadByOrganizationId]",
                    new { OrganizationId = organizationId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task<ICollection<SubvaultCipher>> GetManyByUserIdCipherIdAsync(Guid userId, Guid cipherId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<SubvaultCipher>(
                    "[dbo].[SubvaultCipher_ReadByUserIdCipherId]",
                    new { UserId = userId, CipherId = cipherId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task UpdateSubvaultsAsync(Guid cipherId, Guid userId, IEnumerable<Guid> subvaultIds)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    "[dbo].[SubvaultCipher_UpdateSubvaults]",
                    new { CipherId = cipherId, UserId = userId, SubvaultIds = subvaultIds.ToGuidIdArrayTVP() },
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task UpdateSubvaultsForAdminAsync(Guid cipherId, Guid organizationId, IEnumerable<Guid> subvaultIds)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.ExecuteAsync(
                    "[dbo].[SubvaultCipher_UpdateSubvaultsAdmin]",
                    new { CipherId = cipherId, OrganizationId = organizationId, SubvaultIds = subvaultIds.ToGuidIdArrayTVP() },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}
