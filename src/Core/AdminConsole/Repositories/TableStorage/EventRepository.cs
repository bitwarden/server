using Azure.Data.Tables;
using Bit.Core.Models.Data;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;

#nullable enable

namespace Bit.Core.Repositories.TableStorage;

public class EventRepository : IEventRepository
{
    private readonly TableClient _tableClient;

    public EventRepository(GlobalSettings globalSettings)
        : this(globalSettings.Events.ConnectionString)
    { }

    public EventRepository(string storageConnectionString)
    {
        var tableClient = new TableServiceClient(storageConnectionString);
        _tableClient = tableClient.GetTableClient("event");
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

    public async Task<PagedResult<IEvent>> GetManyBySecretAsync(Secret secret,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"OrganizationId={secret.OrganizationId}",
            $"SecretId={secret.Id}__Date={{0}}", startDate, endDate, pageOptions);
    }

    public async Task<PagedResult<IEvent>> GetManyByProjectAsync(Project project,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return await GetManyAsync($"OrganizationId={project.OrganizationId}",
            $"ProjectId={project.Id}__Date={{0}}", startDate, endDate, pageOptions);
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

    public async Task<PagedResult<IEvent>> GetManyByOrganizationServiceAccountAsync(
        Guid organizationId,
        Guid serviceAccountId,
        DateTime startDate,
        DateTime endDate,
        PageOptions pageOptions)
    {
        return await GetManyServiceAccountAsync(
               $"OrganizationId={organizationId}",
               serviceAccountId.ToString(),
               startDate, endDate, pageOptions);

    }

    public async Task CreateAsync(IEvent e)
    {
        if (!(e is EventTableEntity entity))
        {
            throw new ArgumentException(nameof(e));
        }

        await CreateEventAsync(entity);
    }

    public async Task CreateManyAsync(IEnumerable<IEvent>? e)
    {
        if (e is null || !e.Any())
        {
            return;
        }

        if (!e.Skip(1).Any())
        {
            await CreateAsync(e.First());
            return;
        }

        var entities = e.OfType<EventTableEntity>();
        var entityGroups = entities.GroupBy(ent => ent.PartitionKey);
        foreach (var group in entityGroups)
        {
            var groupEntities = group.ToList();
            if (groupEntities.Count == 1)
            {
                await CreateEventAsync(groupEntities.First());
                continue;
            }

            // A batch insert can only contain 100 entities at a time
            var iterations = groupEntities.Count / 100;
            for (var i = 0; i <= iterations; i++)
            {
                var batch = new List<TableTransactionAction>();
                var batchEntities = groupEntities.Skip(i * 100).Take(100);
                if (!batchEntities.Any())
                {
                    break;
                }

                foreach (var entity in batchEntities)
                {
                    batch.Add(new TableTransactionAction(TableTransactionActionType.Add,
                        entity.ToAzureEvent()));
                }

                await _tableClient.SubmitTransactionAsync(batch);
            }
        }
    }

    public async Task<PagedResult<IEvent>> GetManyServiceAccountAsync(
        string partitionKey,
        string serviceAccountId,
        DateTime startDate,
        DateTime endDate,
        PageOptions pageOptions)
    {
        var start = CoreHelpers.DateTimeToTableStorageKey(startDate);
        var end = CoreHelpers.DateTimeToTableStorageKey(endDate);
        var filter = MakeFilterForServiceAccount(partitionKey, serviceAccountId, startDate, endDate);

        var result = new PagedResult<IEvent>();
        var query = _tableClient.QueryAsync<AzureEvent>(filter, pageOptions.PageSize);

        await using (var enumerator = query.AsPages(pageOptions.ContinuationToken,
            pageOptions.PageSize).GetAsyncEnumerator())
        {
            if (await enumerator.MoveNextAsync())
            {
                result.ContinuationToken = enumerator.Current.ContinuationToken;

                var events = enumerator.Current.Values
                    .Select(e => e.ToEventTableEntity())
                    .ToList();

                events = events.OrderBy(e => e.Date).ToList();

                result.Data.AddRange(events);
            }
        }

        return result;
    }

    public async Task<PagedResult<IEvent>> GetManyAsync(string partitionKey, string rowKey,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        var start = CoreHelpers.DateTimeToTableStorageKey(startDate);
        var end = CoreHelpers.DateTimeToTableStorageKey(endDate);
        var filter = MakeFilter(partitionKey, string.Format(rowKey, start), string.Format(rowKey, end));

        var result = new PagedResult<IEvent>();
        var query = _tableClient.QueryAsync<AzureEvent>(filter, pageOptions.PageSize);

        await using (var enumerator = query.AsPages(pageOptions.ContinuationToken,
            pageOptions.PageSize).GetAsyncEnumerator())
        {
            await enumerator.MoveNextAsync();

            result.ContinuationToken = enumerator.Current.ContinuationToken;
            result.Data.AddRange(enumerator.Current.Values.Select(e => e.ToEventTableEntity()));
        }

        return result;
    }

    private async Task CreateEventAsync(EventTableEntity entity)
    {
        await _tableClient.UpsertEntityAsync(entity.ToAzureEvent());
    }

    private string MakeFilter(string partitionKey, string rowStart, string rowEnd)
    {
        return $"PartitionKey eq '{partitionKey}' and RowKey le '{rowStart}' and RowKey ge '{rowEnd}'";
    }

    private string MakeFilterForServiceAccount(
        string partitionKey,
        string machineAccountId,
        DateTime startDate,
        DateTime endDate)
    {
        var start = CoreHelpers.DateTimeToTableStorageKey(startDate);
        var end = CoreHelpers.DateTimeToTableStorageKey(endDate);

        var rowKey1Start = $"ServiceAccountId={machineAccountId}__Date={start}";
        var rowKey1End = $"ServiceAccountId={machineAccountId}__Date={end}";

        var rowKey2Start = $"GrantedServiceAccountId={machineAccountId}__Date={start}";
        var rowKey2End = $"GrantedServiceAccountId={machineAccountId}__Date={end}";

        var left = $"PartitionKey eq '{partitionKey}' and RowKey le '{rowKey1Start}' and RowKey ge '{rowKey1End}'";
        var right = $"PartitionKey eq '{partitionKey}' and RowKey le '{rowKey2Start}' and RowKey ge '{rowKey2End}'";

        return $"({left}) or ({right})";
    }


}
