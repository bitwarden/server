using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class EventRepository : Repository<Event, Guid>, IEventRepository
{
    public EventRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public EventRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<PagedResult<IEvent>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate,
        PageOptions pageOptions)
    {
        return await GetManyAsync($"[{Schema}].[Event_ReadPageByUserId]",
            new Dictionary<string, object>
            {
                ["@UserId"] = userId
            }, startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByOrganizationAsync(Guid organizationId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"[{Schema}].[Event_ReadPageByOrganizationId]",
            new Dictionary<string, object>
            {
                ["@OrganizationId"] = organizationId
            }, startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByOrganizationActingUserAsync(Guid organizationId, Guid actingUserId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"[{Schema}].[Event_ReadPageByOrganizationIdActingUserId]",
            new Dictionary<string, object>
            {
                ["@OrganizationId"] = organizationId,
                ["@ActingUserId"] = actingUserId
            }, startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByProviderAsync(Guid providerId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"[{Schema}].[Event_ReadPageByProviderId]",
            new Dictionary<string, object>
            {
                ["@ProviderId"] = providerId
            }, startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByProviderActingUserAsync(Guid providerId, Guid actingUserId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"[{Schema}].[Event_ReadPageByProviderIdActingUserId]",
            new Dictionary<string, object>
            {
                ["@ProviderId"] = providerId,
                ["@ActingUserId"] = actingUserId
            }, startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByCipherAsync(Cipher cipher, DateTime startDate, DateTime endDate,
        PageOptions pageOptions)
    {
        return await GetManyAsync($"[{Schema}].[Event_ReadPageByCipherId]",
            new Dictionary<string, object>
            {
                ["@OrganizationId"] = cipher.OrganizationId,
                ["@UserId"] = cipher.UserId,
                ["@CipherId"] = cipher.Id
            }, startDate, endDate, pageOptions);
    }

    public async Task CreateAsync(IEvent e)
    {
        if (!(e is Event ev))
        {
            ev = new Event(e);
        }

        await base.CreateAsync(ev);
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> entities)
    {
        if (!entities?.Any() ?? true)
        {
            return;
        }

        if (!entities.Skip(1).Any())
        {
            await CreateAsync(entities.First());
            return;
        }

        using (var connection = new SqlConnection(ConnectionString))
        {
            connection.Open();
            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, null))
            {
                bulkCopy.DestinationTableName = "[dbo].[Event]";
                var dataTable = BuildEventsTable(bulkCopy, entities.Select(e => e is Event ? e as Event : new Event(e)));
                await bulkCopy.WriteToServerAsync(dataTable);
            }
        }
    }

    private async Task<PagedResult<IEvent>> GetManyAsync(string sprocName,
        IDictionary<string, object> sprocParams, DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        DateTime? beforeDate = null;
        if (!string.IsNullOrWhiteSpace(pageOptions.ContinuationToken) &&
            long.TryParse(pageOptions.ContinuationToken, out var binaryDate))
        {
            beforeDate = DateTime.SpecifyKind(DateTime.FromBinary(binaryDate), DateTimeKind.Utc);
        }

        var parameters = new DynamicParameters(sprocParams);
        parameters.Add("@PageSize", pageOptions.PageSize, DbType.Int32);
        // Explicitly use DbType.DateTime2 for proper precision.
        // ref: https://github.com/StackExchange/Dapper/issues/229
        parameters.Add("@StartDate", startDate.ToUniversalTime(), DbType.DateTime2, null, 7);
        parameters.Add("@EndDate", endDate.ToUniversalTime(), DbType.DateTime2, null, 7);
        parameters.Add("@BeforeDate", beforeDate, DbType.DateTime2, null, 7);

        using (var connection = new SqlConnection(ConnectionString))
        {
            var events = (await connection.QueryAsync<Event>(sprocName, parameters,
                commandType: CommandType.StoredProcedure)).ToList();

            var result = new PagedResult<IEvent>();
            if (events.Any() && events.Count >= pageOptions.PageSize)
            {
                result.ContinuationToken = events.Last().Date.ToBinary().ToString();
            }
            result.Data.AddRange(events);
            return result;
        }
    }

    private DataTable BuildEventsTable(SqlBulkCopy bulkCopy, IEnumerable<Event> events)
    {
        var e = events.FirstOrDefault();
        if (e == null)
        {
            throw new ApplicationException("Must have some events to bulk import.");
        }

        var eventsTable = new DataTable("EventDataTable");

        var idColumn = new DataColumn(nameof(e.Id), e.Id.GetType());
        eventsTable.Columns.Add(idColumn);
        var typeColumn = new DataColumn(nameof(e.Type), typeof(int));
        eventsTable.Columns.Add(typeColumn);
        var userIdColumn = new DataColumn(nameof(e.UserId), typeof(Guid));
        eventsTable.Columns.Add(userIdColumn);
        var organizationIdColumn = new DataColumn(nameof(e.OrganizationId), typeof(Guid));
        eventsTable.Columns.Add(organizationIdColumn);
        var cipherIdColumn = new DataColumn(nameof(e.CipherId), typeof(Guid));
        eventsTable.Columns.Add(cipherIdColumn);
        var collectionIdColumn = new DataColumn(nameof(e.CollectionId), typeof(Guid));
        eventsTable.Columns.Add(collectionIdColumn);
        var policyIdColumn = new DataColumn(nameof(e.PolicyId), typeof(Guid));
        eventsTable.Columns.Add(policyIdColumn);
        var groupIdColumn = new DataColumn(nameof(e.GroupId), typeof(Guid));
        eventsTable.Columns.Add(groupIdColumn);
        var organizationUserIdColumn = new DataColumn(nameof(e.OrganizationUserId), typeof(Guid));
        eventsTable.Columns.Add(organizationUserIdColumn);
        var actingUserIdColumn = new DataColumn(nameof(e.ActingUserId), typeof(Guid));
        eventsTable.Columns.Add(actingUserIdColumn);
        var deviceTypeColumn = new DataColumn(nameof(e.DeviceType), typeof(int));
        eventsTable.Columns.Add(deviceTypeColumn);
        var ipAddressColumn = new DataColumn(nameof(e.IpAddress), typeof(string));
        eventsTable.Columns.Add(ipAddressColumn);
        var dateColumn = new DataColumn(nameof(e.Date), typeof(DateTime));
        eventsTable.Columns.Add(dateColumn);

        foreach (DataColumn col in eventsTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
        }

        var keys = new DataColumn[1];
        keys[0] = idColumn;
        eventsTable.PrimaryKey = keys;

        foreach (var ev in events)
        {
            ev.SetNewId();

            var row = eventsTable.NewRow();

            row[idColumn] = ev.Id;
            row[typeColumn] = (int)ev.Type;
            row[userIdColumn] = ev.UserId.HasValue ? (object)ev.UserId.Value : DBNull.Value;
            row[organizationIdColumn] = ev.OrganizationId.HasValue ? (object)ev.OrganizationId.Value : DBNull.Value;
            row[cipherIdColumn] = ev.CipherId.HasValue ? (object)ev.CipherId.Value : DBNull.Value;
            row[collectionIdColumn] = ev.CollectionId.HasValue ? (object)ev.CollectionId.Value : DBNull.Value;
            row[policyIdColumn] = ev.PolicyId.HasValue ? (object)ev.PolicyId.Value : DBNull.Value;
            row[groupIdColumn] = ev.GroupId.HasValue ? (object)ev.GroupId.Value : DBNull.Value;
            row[organizationUserIdColumn] = ev.OrganizationUserId.HasValue ?
                (object)ev.OrganizationUserId.Value : DBNull.Value;
            row[actingUserIdColumn] = ev.ActingUserId.HasValue ? (object)ev.ActingUserId.Value : DBNull.Value;
            row[deviceTypeColumn] = ev.DeviceType.HasValue ? (object)ev.DeviceType.Value : DBNull.Value;
            row[ipAddressColumn] = ev.IpAddress != null ? (object)ev.IpAddress : DBNull.Value;
            row[dateColumn] = ev.Date;

            eventsTable.Rows.Add(row);
        }

        return eventsTable;
    }
}
