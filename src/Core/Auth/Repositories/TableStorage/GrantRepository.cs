using System.Net;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Settings;
using Microsoft.Azure.Cosmos.Table;

namespace Bit.Core.Auth.Repositories.TableStorage;

public class GrantRepository : IGrantRepository
{
    private readonly CloudTable[] _tables;

    public GrantRepository(GlobalSettings globalSettings)
        : this(globalSettings.IdentityServer.StorageConnectionStrings)
    { }

    public GrantRepository(string[] storageConnectionStrings)
    {
        _tables = new CloudTable[storageConnectionStrings.Length];
        for (var i = 0; i < storageConnectionStrings.Length; i++)
        {
            var storageAccount = CloudStorageAccount.Parse(storageConnectionStrings[i]);
            var tableClient = storageAccount.CreateCloudTableClient();
            _tables[i] = tableClient.GetTableReference("grant");
        }
    }

    public async Task<IGrant> GetByKeyAsync(string key)
    {
        try
        {
            var (partitionKey, rowKey) = GrantTableEntity.CreateTableEntityKeys(key);
            var table = GetTableShard(key);
            var result = await table.ExecuteAsync(TableOperation.Retrieve<GrantTableEntity>(partitionKey, rowKey));
            var entity = result.Result as GrantTableEntity;
            return entity;
        }
        catch (StorageException e) when (e.RequestInformation.HttpStatusCode != (int)HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    public Task<ICollection<IGrant>> GetManyAsync(string subjectId, string sessionId, string clientId, string type)
        => throw new NotImplementedException();

    public async Task SaveAsync(IGrant obj)
    {
        if (obj is not GrantTableEntity entity)
        {
            entity = new GrantTableEntity(obj);
        }

        var table = GetTableShard(obj);
        await table.ExecuteAsync(TableOperation.InsertOrReplace(entity));
    }

    public async Task DeleteByKeyAsync(string key)
    {
        try
        {
            var (partitionKey, rowKey) = GrantTableEntity.CreateTableEntityKeys(key);
            var entity = new TableEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                ETag = "*"
            };
            var table = GetTableShard(key);
            await table.ExecuteAsync(TableOperation.Delete(entity));
        }
        catch (StorageException e) when (e.RequestInformation.HttpStatusCode != (int)HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    public Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type)
        => throw new NotImplementedException();

    private CloudTable GetTableShard(IGrant grant)
    {
        return GetTableShard(grant.Key);
    }

    private CloudTable GetTableShard(string key)
    {
        var keyInt = Convert.ToInt32(key[0]);
        var shardNumber = keyInt % _tables.Length;
        return _tables[shardNumber];
    }
}
