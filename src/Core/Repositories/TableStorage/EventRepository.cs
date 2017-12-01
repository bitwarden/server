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
        public EventRepository(GlobalSettings globalSettings)
        {
            var storageAccount = CloudStorageAccount.Parse(globalSettings.Storage.ConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            Table = tableClient.GetTableReference("event");
        }

        protected CloudTable Table { get; set; }

        public async Task<ICollection<EventTableEntity>> GetManyByUserAsync(Guid userId,
            DateTime startDate, DateTime endDate)
        {
            var start = CoreHelpers.DateTimeToTableStorageKey(startDate);
            var end = CoreHelpers.DateTimeToTableStorageKey(endDate);

            var rowFilter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThanOrEqual, $"{start}_"),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThanOrEqual, $"{end}`"));

            var filter = TableQuery.CombineFilters(
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, $"UserId={userId}"),
                TableOperators.And,
                rowFilter);

            var query = new TableQuery<EventTableEntity>().Where(filter);
            var results = new List<EventTableEntity>();
            TableContinuationToken continuationToken = null;
            do
            {
                var queryResults = await Table.ExecuteQuerySegmentedAsync(query, continuationToken);
                continuationToken = queryResults.ContinuationToken;
                results.AddRange(queryResults.Results);
            } while(continuationToken != null);

            return results;
        }

        public async Task CreateAsync(ITableEntity entity)
        {
            await Table.ExecuteAsync(TableOperation.Insert(entity));
        }

        public async Task CreateManyAsync(IEnumerable<ITableEntity> entities)
        {
            if(!entities?.Any() ?? true)
            {
                return;
            }

            // A batch insert can only contain 100 entities at a time
            var iterations = entities.Count() / 100;
            for(var i = 0; i <= iterations; i++)
            {
                var batch = new TableBatchOperation();
                var batchEntities = entities.Skip(i * 100).Take(100);
                if(!batchEntities.Any())
                {
                    break;
                }

                foreach(var entity in batchEntities)
                {
                    batch.InsertOrReplace(entity);
                }

                await Table.ExecuteBatchAsync(batch);
            }
        }
    }
}
