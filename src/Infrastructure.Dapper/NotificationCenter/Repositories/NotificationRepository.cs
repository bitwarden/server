#nullable enable
using System.Data;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
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

    public async Task<PagedResult<NotificationStatusDetails>> GetByUserIdAndStatusAsync(Guid userId,
        ClientType clientType, NotificationStatusFilter? statusFilter, PageOptions pageOptions)
    {
        await using var connection = new SqlConnection(ConnectionString);

        if (!int.TryParse(pageOptions.ContinuationToken, out var pageNumber))
        {
            pageNumber = 1;
        }

        var results = await connection.QueryAsync<NotificationStatusDetails>(
            "[dbo].[Notification_ReadByUserIdAndStatus]",
            new
            {
                UserId = userId,
                ClientType = clientType,
                statusFilter?.Read,
                statusFilter?.Deleted,
                PageNumber = pageNumber,
                pageOptions.PageSize
            },
            commandType: CommandType.StoredProcedure);

        var data = results.ToList();

        return new PagedResult<NotificationStatusDetails>
        {
            Data = data,
            ContinuationToken = data.Count < pageOptions.PageSize ? null : (pageNumber + 1).ToString()
        };
    }
}
