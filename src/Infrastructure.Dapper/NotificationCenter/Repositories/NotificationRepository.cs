#nullable enable
using System.Data;
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Models.Data;
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

    public async Task<IEnumerable<NotificationStatusDetails>> GetByUserIdAndStatusAsync(Guid userId,
        ClientType clientType, NotificationStatusFilter? statusFilter)
    {
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<NotificationStatusDetails>(
            "[dbo].[Notification_ReadByUserIdAndStatus]",
            new { UserId = userId, ClientType = clientType, statusFilter?.Read, statusFilter?.Deleted },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }
}
