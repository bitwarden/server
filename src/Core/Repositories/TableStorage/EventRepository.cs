using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Repositories.TableStorage
{
    public class EventRepository : IEventRepository
    {
        private readonly CloudTable _table;

        public EventRepository(GlobalSettings globalSettings)
            : this(globalSettings.Storage.ConnectionString)
        { }

        public EventRepository(string storageConnectionString)
        {
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            _table = tableClient.GetTableReference("event");
        }

        public async Task<ICollection<IEvent>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate)
        {
            var start = CoreHelpers.DateTimeToTableStorageKey(startDate);
            var end = CoreHelpers.DateTimeToTableStorageKey(endDate);

            var rowFilter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThanOrEqual, $"Date={start}_"),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThanOrEqual, $"Date={end}`"));

            var filter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, $"UserId={userId}"),
                TableOperators.And,
                rowFilter);

            var query = new TableQuery<EventTableEntity>().Where(filter);
            var results = new List<EventTableEntity>();
            TableContinuationToken continuationToken = null;
            do
            {
                var queryResults = await _table.ExecuteQuerySegmentedAsync(query, continuationToken);
                continuationToken = queryResults.ContinuationToken;
                results.AddRange(queryResults.Results);
            }
            while(continuationToken != null);

            return results.Select(r => r as IEvent).ToList();
        }

        public async Task<ICollection<IEvent>> GetManyByOrganizationAsync(Guid organizationId,
            DateTime startDate, DateTime endDate)
        {
            var start = CoreHelpers.DateTimeToTableStorageKey(startDate);
            var end = CoreHelpers.DateTimeToTableStorageKey(endDate);

            var rowFilter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThanOrEqual, $"Date={start}_"),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThanOrEqual, $"Date={end}`"));

            var filter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, $"OrganizationId={organizationId}"),
                TableOperators.And,
                rowFilter);

            var query = new TableQuery<EventTableEntity>().Where(filter);
            var results = new List<EventTableEntity>();
            TableContinuationToken continuationToken = null;
            do
            {
                var queryResults = await _table.ExecuteQuerySegmentedAsync(query, continuationToken);
                continuationToken = queryResults.ContinuationToken;
                results.AddRange(queryResults.Results);
            }
            while(continuationToken != null);

            return results.Select(r => r as IEvent).ToList();
        }

        public async Task CreateAsync(IEvent e)
        {
            if(!(e is EventTableEntity entity))
            {
                throw new ArgumentException(nameof(e));
            }

            await CreateEntityAsync(entity);
        }

        public async Task CreateManyAsync(IList<IEvent> e)
        {
            if(!e?.Any() ?? true)
            {
                return;
            }

            if(e.Count == 1)
            {
                await CreateAsync(e.First());
                return;
            }

            var entities = e.Where(ev => ev is EventTableEntity).Select(ev => ev as EventTableEntity);
            var entityGroups = entities.GroupBy(ent => ent.PartitionKey);
            foreach(var group in entityGroups)
            {
                var groupEntities = group.ToList();
                if(groupEntities.Count == 1)
                {
                    await CreateEntityAsync(groupEntities.First());
                    continue;
                }

                // A batch insert can only contain 100 entities at a time
                var iterations = groupEntities.Count / 100;
                for(var i = 0; i <= iterations; i++)
                {
                    var batch = new TableBatchOperation();
                    var batchEntities = groupEntities.Skip(i * 100).Take(100);
                    if(!batchEntities.Any())
                    {
                        break;
                    }

                    foreach(var entity in batchEntities)
                    {
                        batch.InsertOrReplace(entity);
                    }

                    await _table.ExecuteBatchAsync(batch);
                }
            }
        }

        public async Task CreateEntityAsync(ITableEntity entity)
        {
            await _table.ExecuteAsync(TableOperation.Insert(entity));
        }
    }
}
