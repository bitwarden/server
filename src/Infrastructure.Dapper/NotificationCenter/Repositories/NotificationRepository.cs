#nullable enable
using System.Data;
using Bit.Core.NotificationCenter.Entities;
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

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, NotificationFilter notificationFilter)
    {
        await using var connection = new SqlConnection(ConnectionString);

        IEnumerable<Notification> results;
        if (notificationFilter.OrganizationIds != null && notificationFilter.OrganizationIds.Any())
        {
            results = await connection.QueryAsync<Notification>(
                "[dbo].[Notification_ReadByUserIdAndOrganizations]",
                new
                {
                    UserId = userId,
                    notificationFilter.ClientType,
                    OrganizationIds = notificationFilter.OrganizationIds.ToGuidIdArrayTVP()
                },
                commandType: CommandType.StoredProcedure);
        }
        else
        {
            results = await connection.QueryAsync<Notification>(
                "[dbo].[Notification_ReadByUserId]",
                new { UserId = userId, notificationFilter.ClientType },
                commandType: CommandType.StoredProcedure);
        }

        return results.ToList();
    }

    public async Task<IEnumerable<Notification>> GetByUserIdAndStatusAsync(Guid userId,
        NotificationFilter notificationFilter,
        NotificationStatusFilter statusFilter)
    {
        await using var connection = new SqlConnection(ConnectionString);

        IEnumerable<Notification> results;
        if (notificationFilter.OrganizationIds != null && notificationFilter.OrganizationIds.Any())
        {
            results = await connection.QueryAsync<Notification>(
                "[dbo].[Notification_ReadByUserIdAndOrganizationsAndStatus]",
                new
                {
                    UserId = userId,
                    notificationFilter.ClientType,
                    OrganizationIds = notificationFilter.OrganizationIds.ToGuidIdArrayTVP(),
                    statusFilter.Read,
                    statusFilter.Deleted
                },
                commandType: CommandType.StoredProcedure);
        }
        else
        {
            results = await connection.QueryAsync<Notification>(
                "[dbo].[Notification_ReadByUserIdAndStatus]",
                new { UserId = userId, notificationFilter.ClientType, statusFilter.Read, statusFilter.Deleted },
                commandType: CommandType.StoredProcedure);
        }

        return results.ToList();
    }
}
