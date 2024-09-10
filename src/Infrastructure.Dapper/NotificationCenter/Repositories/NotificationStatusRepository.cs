#nullable enable
using System.Data;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.NotificationCenter.Repositories;

public class NotificationStatusRepository : BaseRepository, INotificationStatusRepository
{
    public NotificationStatusRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public NotificationStatusRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<NotificationStatus?> GetByNotificationIdAndUserIdAsync(Guid notificationId, Guid userId)
    {
        await using var connection = new SqlConnection(ConnectionString);

        return await connection.QueryFirstOrDefaultAsync<NotificationStatus>(
            "[dbo].[NotificationStatus_ReadByNotificationIdAndUserId]",
            new { NotificationId = notificationId, UserId = userId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<NotificationStatus> CreateAsync(NotificationStatus notificationStatus)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync("[dbo].[NotificationStatus_Create]",
            notificationStatus, commandType: CommandType.StoredProcedure);

        return notificationStatus;
    }

    public async Task UpdateAsync(NotificationStatus notificationStatus)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync("[dbo].[NotificationStatus_Update]",
            notificationStatus, commandType: CommandType.StoredProcedure);
    }
}
