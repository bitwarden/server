using System;
using Bit.Core.Models.Table;
using System.Threading.Tasks;
using System.Collections.Generic;
using Bit.Core.Models.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Data;

namespace Bit.Core.Repositories.SqlServer
{
    public class EventRepository : Repository<Event, Guid>, IEventRepository
    {
        public EventRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString)
        { }

        public EventRepository(string connectionString)
            : base(connectionString)
        { }

        public Task<ICollection<IEvent>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            // TODO
            throw new NotImplementedException();
        }

        public Task<ICollection<IEvent>> GetManyByOrganizationAsync(Guid organizationId, DateTime startDate, DateTime endDate)
        {
            throw new NotImplementedException();
        }

        public async Task CreateAsync(IEvent e)
        {
            if(!(e is Event ev))
            {
                ev = new Event(e);
            }

            await base.CreateAsync(ev);
        }

        public async Task CreateManyAsync(IList<IEvent> entities)
        {
            if(!entities?.Any() ?? true)
            {
                return;
            }

            if(entities.Count == 1)
            {
                await CreateAsync(entities.First());
                return;
            }

            using(var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using(var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.KeepIdentity, null))
                {
                    bulkCopy.DestinationTableName = "[dbo].[Event]";
                    var dataTable = BuildEventsTable(entities.Select(e => e is Event ? e as Event : new Event(e)));
                    await bulkCopy.WriteToServerAsync(dataTable);
                }
            }
        }

        private DataTable BuildEventsTable(IEnumerable<Event> events)
        {
            var e = events.FirstOrDefault();
            if(e == null)
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
            var groupIdColumn = new DataColumn(nameof(e.GroupId), typeof(Guid));
            eventsTable.Columns.Add(groupIdColumn);
            var actingUserIdColumn = new DataColumn(nameof(e.ActingUserId), typeof(Guid));
            eventsTable.Columns.Add(actingUserIdColumn);
            var organizationUserIdColumn = new DataColumn(nameof(e.OrganizationUserId), typeof(Guid));
            eventsTable.Columns.Add(organizationUserIdColumn);
            var dateColumn = new DataColumn(nameof(e.Date), e.Date.GetType());
            eventsTable.Columns.Add(dateColumn);

            var keys = new DataColumn[1];
            keys[0] = idColumn;
            eventsTable.PrimaryKey = keys;

            foreach(var ev in events)
            {
                ev.SetNewId();

                var row = eventsTable.NewRow();

                row[idColumn] = ev.Id;
                row[typeColumn] = (int)ev.Type;
                row[dateColumn] = ev.Date;
                row[userIdColumn] = ev.UserId.HasValue ? (object)ev.UserId.Value : DBNull.Value;
                row[organizationIdColumn] = ev.OrganizationId.HasValue ? (object)ev.OrganizationId.Value : DBNull.Value;
                row[cipherIdColumn] = ev.CipherId.HasValue ? (object)ev.CipherId.Value : DBNull.Value;
                row[groupIdColumn] = ev.GroupId.HasValue ? (object)ev.GroupId.Value : DBNull.Value;
                row[collectionIdColumn] = ev.CollectionId.HasValue ? (object)ev.CollectionId.Value : DBNull.Value;
                row[actingUserIdColumn] = ev.ActingUserId.HasValue ? (object)ev.ActingUserId.Value : DBNull.Value;
                row[organizationUserIdColumn] = ev.OrganizationUserId.HasValue ?
                    (object)ev.OrganizationUserId.Value : DBNull.Value;

                eventsTable.Rows.Add(row);
            }

            return eventsTable;
        }
    }
}
