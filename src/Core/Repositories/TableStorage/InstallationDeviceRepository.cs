using Azure.Data.Tables;
using Bit.Core.Models.Data;
using Bit.Core.Settings;

#nullable enable

namespace Bit.Core.Repositories.TableStorage;

public class InstallationDeviceRepository : IInstallationDeviceRepository
{
    private readonly TableClient _tableClient;

    public InstallationDeviceRepository(GlobalSettings globalSettings)
        : this(globalSettings.Events.ConnectionString)
    { }

    public InstallationDeviceRepository(string storageConnectionString)
    {
        var tableClient = new TableServiceClient(storageConnectionString);
        _tableClient = tableClient.GetTableClient("installationdevice");
    }

    public async Task UpsertAsync(InstallationDeviceEntity entity)
    {
        await _tableClient.UpsertEntityAsync(entity);
    }

    public async Task UpsertManyAsync(IList<InstallationDeviceEntity>? entities)
    {
        if (entities is null || !entities.Any())
        {
            return;
        }

        if (entities.Count == 1)
        {
            await UpsertAsync(entities.First());
            return;
        }

        var entityGroups = entities.GroupBy(ent => ent.PartitionKey);
        foreach (var group in entityGroups)
        {
            var groupEntities = group.ToList();
            if (groupEntities.Count == 1)
            {
                await UpsertAsync(groupEntities.First());
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
                    batch.Add(new TableTransactionAction(TableTransactionActionType.UpsertReplace, entity));
                }

                await _tableClient.SubmitTransactionAsync(batch);
            }
        }
    }

    public async Task DeleteAsync(InstallationDeviceEntity entity)
    {
        await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey);
    }
}
