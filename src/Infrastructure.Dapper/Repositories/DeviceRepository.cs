using System.Data;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.KeyManagement.UserKey;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Repositories;

public class DeviceRepository : Repository<Device, Guid>, IDeviceRepository
{
    private readonly IGlobalSettings _globalSettings;

    public DeviceRepository(GlobalSettings globalSettings)
        : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
        _globalSettings = globalSettings;
    }

    public async Task<Device?> GetByIdAsync(Guid id, Guid userId)
    {
        var device = await GetByIdAsync(id);
        if (device == null || device.UserId != userId)
        {
            return null;
        }

        return device;
    }

    public async Task<Device?> GetByIdentifierAsync(string identifier)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Device>(
                $"[{Schema}].[{Table}_ReadByIdentifier]",
                new
                {
                    Identifier = identifier
                },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

    public async Task<Device?> GetByIdentifierAsync(string identifier, Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Device>(
                $"[{Schema}].[{Table}_ReadByIdentifierUserId]",
                new
                {
                    UserId = userId,
                    Identifier = identifier
                },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

    public async Task<ICollection<Device>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Device>(
                $"[{Schema}].[{Table}_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<DeviceAuthDetails>> GetManyByUserIdWithDeviceAuth(Guid userId)
    {
        var expirationMinutes = _globalSettings.PasswordlessAuth.UserRequestExpiration.TotalMinutes;
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<DeviceAuthDetails>(
                $"[{Schema}].[{Table}_ReadActiveWithPendingAuthRequestsByUserId]",
                new
                {
                    UserId = userId,
                    ExpirationMinutes = expirationMinutes
                },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task ClearPushTokenAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                $"[{Schema}].[{Table}_ClearPushTokenById]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);
        }
    }

    public UpdateEncryptedDataForKeyRotation UpdateKeysForRotationAsync(Guid userId, IEnumerable<Device> devices)
    {
        return async (SqlConnection connection, SqlTransaction transaction) =>
        {
            const string sql = @"
                UPDATE D
                SET
                    D.[EncryptedPublicKey] = UD.[encryptedPublicKey],
                    D.[EncryptedUserKey] = UD.[encryptedUserKey]
                FROM
                    [dbo].[Device] D
                INNER JOIN
                    OPENJSON(@DeviceCredentials)
                    WITH (
                        id UNIQUEIDENTIFIER,
                        encryptedPublicKey NVARCHAR(MAX),
                        encryptedUserKey NVARCHAR(MAX)
                    ) UD
                    ON UD.[id] = D.[Id]
                WHERE
                    D.[UserId] = @UserId";
            var deviceCredentials = CoreHelpers.ClassToJsonData(devices);

            await connection.ExecuteAsync(
                sql,
                new { UserId = userId, DeviceCredentials = deviceCredentials },
                transaction: transaction,
                commandType: CommandType.Text);
        };
    }
}
