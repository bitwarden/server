using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class DeviceRepository : Repository<Device, Guid>, IDeviceRepository
{
    public DeviceRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public DeviceRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<Device> GetByIdAsync(Guid id, Guid userId)
    {
        var device = await GetByIdAsync(id);
        if (device == null || device.UserId != userId)
        {
            return null;
        }

        return device;
    }

    public async Task<Device> GetByIdentifierAsync(string identifier)
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

    public async Task<Device> GetByIdentifierAsync(string identifier, Guid userId)
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
}
