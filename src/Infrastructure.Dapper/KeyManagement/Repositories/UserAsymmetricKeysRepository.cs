#nullable enable
using System.Data;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.KeyManagement.Repositories;

public class UserAsymmetricKeysRepository : BaseRepository, IUserAsymmetricKeysRepository
{
    public UserAsymmetricKeysRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public UserAsymmetricKeysRepository(string connectionString, string readOnlyConnectionString) : base(
        connectionString, readOnlyConnectionString)
    {
    }

    public async Task RegenerateUserAsymmetricKeysAsync(UserAsymmetricKeys userAsymmetricKeys,
        IEnumerable<DatabaseTransactionAction> updateDataActions)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        try
        {
            await connection.ExecuteAsync("[dbo].[UserAsymmetricKeys_Regenerate]",
                new
                {
                    userAsymmetricKeys.UserId,
                    userAsymmetricKeys.PublicKey,
                    PrivateKey = userAsymmetricKeys.UserKeyEncryptedPrivateKey
                }, transaction: transaction, commandType: CommandType.StoredProcedure);

            foreach (var action in updateDataActions)
            {
                await action(connection, transaction);
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
