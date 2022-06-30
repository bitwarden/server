using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Azure.Cosmos.Table;

namespace Bit.Core.Repositories.TableStorage;

public class EventRepository : IEventRepository
{
    private readonly CloudTable _table;

    public EventRepository(GlobalSettings globalSettings)
        : this(globalSettings.Events.ConnectionString)
    { }

    public EventRepository(string storageConnectionString)
    {
        var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
        var tableClient = storageAccount.CreateCloudTableClient();
        _table = tableClient.GetTableReference("event");
    }

    public async Task<PagedResult<IEvent>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate,
        PageOptions pageOptions)
    {
        return await GetManyAsync($"UserId={userId}", "Date={{0}}", startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByOrganizationAsync(Guid organizationId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"OrganizationId={organizationId}", "Date={0}", startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByOrganizationActingUserAsync(Guid organizationId, Guid actingUserId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"OrganizationId={organizationId}",
            $"ActingUserId={actingUserId}__Date={{0}}", startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByProviderAsync(Guid providerId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"ProviderId={providerId}", "Date={0}", startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByProviderActingUserAsync(Guid providerId, Guid actingUserId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"ProviderId={providerId}",
            $"ActingUserId={actingUserId}__Date={{0}}", startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByCipherAsync(Cipher cipher, DateTime startDate, DateTime endDate,
        PageOptions pageOptions)
    {
        var partitionKey = cipher.OrganizationId.HasValue ?
            $"OrganizationId={cipher.OrganizationId}" : $"UserId={cipher.UserId}";
        return await GetManyAsync(partitionKey, $"CipherId={cipher.Id}__Date={{0}}", startDate, endDate, pageOptions);
    }

    public async Task CreateAsync(IEvent e)
    {
        if (!(e is EventTableEntity entity))
        {
            throw new ArgumentException(nameof(e));
        }

        await CreateEntityAsync(entity);
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> e)
    {
        if (!e?.Any() ?? true)
        {
            return;
        }

        if (!e.Skip(1).Any())
        {
            await CreateAsync(e.First());
            return;
        }

        var entities = e.Where(ev => ev is EventTableEntity).Select(ev => ev as EventTableEntity);
        var entityGroups = entities.GroupBy(ent => ent.PartitionKey);
        foreach (var group in entityGroups)
        {
            var groupEntities = group.ToList();
            if (groupEntities.Count == 1)
            {
                await CreateEntityAsync(groupEntities.First());
                continue;
            }

            // A batch insert can only contain 100 entities at a time
            var iterations = groupEntities.Count / 100;
            for (var i = 0; i <= iterations; i++)
            {
                var batch = new TableBatchOperation();
                var batchEntities = groupEntities.Skip(i * 100).Take(100);
                if (!batchEntities.Any())
                {
                    break;
                }

                foreach (var entity in batchEntities)
                {
                    batch.InsertOrReplace(entity);
                }

                await _table.ExecuteBatchAsync(batch);
            }
        }
    }

    public async Task CreateEntityAsync(ITableEntity entity)
    {
        await _table.ExecuteAsync(TableOperation.InsertOrReplace(entity));
    }

    public async Task<PagedResult<IEvent>> GetManyAsync(string partitionKey, string rowKey,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        var start = CoreHelpers.DateTimeToTableStorageKey(startDate);
        var end = CoreHelpers.DateTimeToTableStorageKey(endDate);
        var filter = MakeFilter(partitionKey, string.Format(rowKey, start), string.Format(rowKey, end));

        var query = new TableQuery<EventTableEntity>().Where(filter).Take(pageOptions.PageSize);
        var result = new PagedResult<IEvent>();
        var continuationToken = DeserializeContinuationToken(pageOptions?.ContinuationToken);

        var queryResults = await _table.ExecuteQuerySegmentedAsync(query, continuationToken);
        result.ContinuationToken = SerializeContinuationToken(queryResults.ContinuationToken);
        result.Data.AddRange(queryResults.Results);

        return result;
    }

    private string MakeFilter(string partitionKey, string rowStart, string rowEnd)
    {
        var rowFilter = TableQuery.CombineFilters(
            TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThanOrEqual, $"{rowStart}`"),
            TableOperators.And,
            TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.GreaterThanOrEqual, $"{rowEnd}_"));

        return TableQuery.CombineFilters(
            TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey),
            TableOperators.And,
            rowFilter);
    }

    private string SerializeContinuationToken(TableContinuationToken token)
    {
        if (token == null)
        {
            return null;
        }

        return string.Format("{0}__{1}__{2}__{3}", (int)token.TargetLocation, token.NextTableName,
            token.NextPartitionKey, token.NextRowKey);
    }

    private TableContinuationToken DeserializeContinuationToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenParts = token.Split(new string[] { "__" }, StringSplitOptions.None);
        if (tokenParts.Length < 4 || !Enum.TryParse(tokenParts[0], out StorageLocation tLoc))
        {
            return null;
        }

        return new TableContinuationToken
        {
            TargetLocation = tLoc,
            NextTableName = string.IsNullOrWhiteSpace(tokenParts[1]) ? null : tokenParts[1],
            NextPartitionKey = string.IsNullOrWhiteSpace(tokenParts[2]) ? null : tokenParts[2],
            NextRowKey = string.IsNullOrWhiteSpace(tokenParts[3]) ? null : tokenParts[3]
        };
    }
}
