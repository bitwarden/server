using System.Net;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Azure.Cosmos.Table;

namespace Bit.Core.Repositories.TableStorage;

public class MetaDataRepository : IMetaDataRepository
{
    private readonly CloudTable _table;

    public MetaDataRepository(GlobalSettings globalSettings)
        : this(globalSettings.Events.ConnectionString)
    { }

    public MetaDataRepository(string storageConnectionString)
    {
        var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
        var tableClient = storageAccount.CreateCloudTableClient();
        _table = tableClient.GetTableReference("metadata");
    }

    public async Task<IDictionary<string, string>> GetAsync(string objectName, string id)
    {
        var query = new TableQuery<DictionaryEntity>().Where(
            TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, $"{objectName}_{id}"));
        var queryResults = await _table.ExecuteQuerySegmentedAsync(query, null);
        return queryResults.Results.FirstOrDefault()?.ToDictionary(d => d.Key, d => d.Value.StringValue);
    }

    public async Task<string> GetAsync(string objectName, string id, string prop)
    {
        var dict = await GetAsync(objectName, id);
        if (dict != null && dict.ContainsKey(prop))
        {
            return dict[prop];
        }
        return null;
    }

    public async Task UpsertAsync(string objectName, string id, KeyValuePair<string, string> keyValuePair)
    {
        var query = new TableQuery<DictionaryEntity>().Where(
            TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, $"{objectName}_{id}"));
        var queryResults = await _table.ExecuteQuerySegmentedAsync(query, null);
        var entity = queryResults.Results.FirstOrDefault();
        if (entity == null)
        {
            entity = new DictionaryEntity
            {
                PartitionKey = $"{objectName}_{id}",
                RowKey = string.Empty
            };
        }
        if (entity.ContainsKey(keyValuePair.Key))
        {
            entity.Remove(keyValuePair.Key);
        }
        entity.Add(keyValuePair.Key, keyValuePair.Value);
        await _table.ExecuteAsync(TableOperation.InsertOrReplace(entity));
    }

    public async Task UpsertAsync(string objectName, string id, IDictionary<string, string> dict)
    {
        var entity = new DictionaryEntity
        {
            PartitionKey = $"{objectName}_{id}",
            RowKey = string.Empty
        };
        foreach (var item in dict)
        {
            entity.Add(item.Key, item.Value);
        }
        await _table.ExecuteAsync(TableOperation.InsertOrReplace(entity));
    }

    public async Task DeleteAsync(string objectName, string id)
    {
        try
        {
            await _table.ExecuteAsync(TableOperation.Delete(new DictionaryEntity
            {
                PartitionKey = $"{objectName}_{id}",
                RowKey = string.Empty,
                ETag = "*"
            }));
        }
        catch (StorageException e) when (e.RequestInformation.HttpStatusCode != (int)HttpStatusCode.NotFound)
        {
            throw;
        }
    }
}
