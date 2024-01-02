using System.Net;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Settings;
using Microsoft.Azure.Cosmos.Table;

namespace Bit.Core.Auth.Repositories.TableStorage;

public class GrantRepository : IGrantRepository
{
    private readonly CloudTable _table;

    public GrantRepository(GlobalSettings globalSettings)
        : this(globalSettings.IdentityServer.StorageConnectionString)
    { }

    public GrantRepository(string storageConnectionString)
    {
        var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
        var tableClient = storageAccount.CreateCloudTableClient();
        _table = tableClient.GetTableReference("grant");
    }

    public async Task<IGrant> GetByKeyAsync(string key)
    {
        try
        {
            var (partitionKey, rowKey) = GrantTableEntity.CreateTableEntityKeys(key);
            var result = await _table.ExecuteAsync(TableOperation.Retrieve<GrantTableEntity>(partitionKey, rowKey));
            var entity = result.Result as GrantTableEntity;
            return entity;
        }
        catch (StorageException e) when (e.RequestInformation.HttpStatusCode != (int)HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    public Task<ICollection<IGrant>> GetManyAsync(string subjectId, string sessionId, string clientId, string type) => throw new NotImplementedException();

    public async Task SaveAsync(IGrant obj)
    {
        if (!(obj is GrantTableEntity entity))
        {
            entity = new GrantTableEntity(obj);
        }

        await _table.ExecuteAsync(TableOperation.InsertOrReplace(entity));
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
            await _table.ExecuteAsync(TableOperation.Delete(entity));
        }
        catch (StorageException e) when (e.RequestInformation.HttpStatusCode != (int)HttpStatusCode.NotFound)
        {
            throw;
        }
    }

    public Task DeleteManyAsync(string subjectId, string sessionId, string clientId, string type) => throw new NotImplementedException();
}
