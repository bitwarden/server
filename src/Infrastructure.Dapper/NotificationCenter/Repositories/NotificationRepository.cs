#nullable enable
using System.Data;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationCenter.Models.Filter;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.NotificationCenter.Repositories;

public class NotificationRepository : Repository<Notification, Guid>, INotificationRepository
{
    public NotificationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public NotificationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    {
    }

    public async Task<IEnumerable<Notification>> GetByUserIdAndStatusAsync(Guid userId,
        ClientType clientType, NotificationStatusFilter? statusFilter)
    {
        await using var connection = new SqlConnection(ConnectionString);

        IEnumerable<Notification> results;
        if (statusFilter != null && (statusFilter.Read != null || statusFilter.Deleted != null))
        {
            results = await connection.QueryAsync<Notification>(
                "[dbo].[Notification_ReadByUserIdAndStatus]",
                new { UserId = userId, ClientType = clientType, statusFilter.Read, statusFilter.Deleted },
                commandType: CommandType.StoredProcedure);
        }
        else
        {
            results = await connection.QueryAsync<Notification>(
                "[dbo].[Notification_ReadByUserId]",
                new { UserId = userId, ClientType = clientType },
                commandType: CommandType.StoredProcedure);
        }

        return results.ToList();
    }
}
