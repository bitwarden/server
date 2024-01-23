using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Microsoft.Azure.Cosmos;

namespace Bit.Core.Repositories.Cosmos;

public class EventRepository : IEventRepository
{
    private readonly CosmosClient _client;
    private readonly Database _database;
    private readonly Container _container;

    public EventRepository(GlobalSettings globalSettings)
        : this("TODO")
    { }

    public EventRepository(string cosmosConnectionString)
    {
        var options = new CosmosClientOptions
        {
            Serializer = new SystemTextJsonCosmosSerializer(new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            }),
            // ref: https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/tutorial-dotnet-bulk-import
            AllowBulkExecution = true
        };
        // TODO: Perhaps we want to evaluate moving this to DI as a keyed service singleton in .NET 8
        _client = new CosmosClient(cosmosConnectionString, options);
        // TODO: Better naming here? Seems odd
        _database = _client.GetDatabase("events");
        _container = _database.GetContainer("events");
    }

    public Task<PagedResult<IEvent>> GetManyByCipherAsync(Cipher cipher,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return PagedQueryAsync("e.cipId = @cipId",
            q => q.WithParameter("@cipId", cipher.Id),
            startDate, endDate, pageOptions);
    }

    public Task<PagedResult<IEvent>> GetManyByOrganizationActingUserAsync(Guid organizationId, Guid actingUserId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return PagedQueryAsync("e.oId = @oId AND e.auId = @auId",
            q => q.WithParameter("@oId", organizationId).WithParameter("@auId", actingUserId),
            startDate, endDate, pageOptions);
    }

    public Task<PagedResult<IEvent>> GetManyByOrganizationAsync(Guid organizationId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return PagedQueryAsync("e.oId = @oId",
            q => q.WithParameter("@oId", organizationId),
            startDate, endDate, pageOptions);
    }

    public Task<PagedResult<IEvent>> GetManyByOrganizationServiceAccountAsync(Guid organizationId, Guid serviceAccountId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return PagedQueryAsync("e.oid = @oid AND e.saId = @saId",
            q => q.WithParameter("@oid", organizationId).WithParameter("@saId", serviceAccountId),
            startDate, endDate, pageOptions);
    }

    public Task<PagedResult<IEvent>> GetManyByProviderActingUserAsync(Guid providerId, Guid actingUserId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return PagedQueryAsync("e.prId = @prId AND e.auId = @auId",
            q => q.WithParameter("@prId", providerId).WithParameter("@auId", actingUserId),
            startDate, endDate, pageOptions);
    }

    public Task<PagedResult<IEvent>> GetManyByProviderAsync(Guid providerId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return PagedQueryAsync("e.prId = @prId",
            q => q.WithParameter("@prId", providerId),
            startDate, endDate, pageOptions);
    }

    public Task<PagedResult<IEvent>> GetManyByUserAsync(Guid userId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        return PagedQueryAsync("e.uId = @uId",
            q => q.WithParameter("@uId", userId),
            startDate, endDate, pageOptions);
    }

    public async Task CreateAsync(IEvent e)
    {
        if (e is not EventItem item)
        {
            item = new EventItem(e);
        }
        // TODO: How should we handle the partition yet? Perhaps something like table storage did with
        // orgId, userId, providerId
        await _container.CreateItemAsync(item, new PartitionKey(item.Id), new ItemRequestOptions
        {
            // ref: https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/best-practice-dotnet#best-practices-for-write-heavy-workloads
            EnableContentResponseOnWrite = false
        });
    }

    public Task CreateManyAsync(IEnumerable<IEvent> events)
    {
        // ref: https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/tutorial-dotnet-bulk-import
        var tasks = new List<Task>();
        foreach (var e in events)
        {
            tasks.Add(CreateAsync(e));
        }
        return Task.WhenAll(tasks);
    }

    private async Task<PagedResult<IEvent>> PagedQueryAsync(string queryFilter,
        Action<QueryDefinition> applyParameters, DateTime startDate, DateTime endDate,
        PageOptions pageOptions)
    {
        var query = new QueryDefinition(
            $"SELECT * FROM events e WHERE {queryFilter} AND e.date >= @startDate AND e.date <= @endDate")
            .WithParameter("@startDate", startDate)
            .WithParameter("@endDate", endDate);

        applyParameters(query);

        using var iterator = _container.GetItemQueryIterator<EventItem>(query, pageOptions.ContinuationToken,
            new QueryRequestOptions
            {
                MaxItemCount = pageOptions.PageSize,
            });

        var result = new PagedResult<IEvent>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            result.Data.AddRange(response);
            if (response.Count > 0)
            {
                result.ContinuationToken = response.ContinuationToken;
                break;
            }
        }
        return result;
    }
}
