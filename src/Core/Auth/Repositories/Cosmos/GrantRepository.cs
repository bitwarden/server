using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Azure.Cosmos;

#nullable enable

namespace Bit.Core.Auth.Repositories.Cosmos;

public class GrantRepository : IGrantRepository
{
    private readonly CosmosClient _client;
    private readonly Database _database;
    private readonly Container _container;

    public GrantRepository(GlobalSettings globalSettings)
        : this(globalSettings.IdentityServer.CosmosConnectionString) { }

    public GrantRepository(string cosmosConnectionString)
    {
        var options = new CosmosClientOptions
        {
            Serializer = new SystemTextJsonCosmosSerializer(
                new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = false,
                }
            ),
        };
        // TODO: Perhaps we want to evaluate moving this to DI as a keyed service singleton in .NET 8
        _client = new CosmosClient(cosmosConnectionString, options);
        _database = _client.GetDatabase("identity");
        _container = _database.GetContainer("grant");
    }

    public async Task<IGrant?> GetByKeyAsync(string key)
    {
        var id = Base64IdStringConverter.ToId(key);
        try
        {
            var response = await _container.ReadItemAsync<GrantItem>(id, new PartitionKey(id));
            return response.Resource;
        }
        catch (CosmosException e)
        {
            if (e.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            throw;
        }
    }

    public Task<ICollection<IGrant>> GetManyAsync(
        string subjectId,
        string sessionId,
        string clientId,
        string type
    ) => throw new NotImplementedException();

    public async Task SaveAsync(IGrant obj)
    {
        if (obj is not GrantItem item)
        {
            item = new GrantItem(obj);
        }
        item.SetTtl();
        var id = Base64IdStringConverter.ToId(item.Key);
        await _container.UpsertItemAsync(
            item,
            new PartitionKey(id),
            new ItemRequestOptions
            {
                // ref: https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/best-practice-dotnet#best-practices-for-write-heavy-workloads
                EnableContentResponseOnWrite = false,
            }
        );
    }

    public async Task DeleteByKeyAsync(string key)
    {
        var id = Base64IdStringConverter.ToId(key);
        await _container.DeleteItemAsync<IGrant>(id, new PartitionKey(id));
    }

    public Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type) =>
        throw new NotImplementedException();
}
